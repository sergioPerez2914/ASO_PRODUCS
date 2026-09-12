using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Materia Prima · Recepciones: el historial de lo que entró y el registro de lo nuevo.
///
/// Las filas son documentos: no se editan ni se borran, se anulan. Por eso la pantalla apaga
/// Editar y Eliminar y solo deja "Registrar recepción" y "Anular".
/// </summary>
public sealed class RecepcionesMateriaPrimaViewModel : PantallaCrudViewModel<RecepcionMateriaPrima, int>
{
    private const string FiltroTodas = "Todas";

    private readonly ITipoMateriaPrimaDataSource _tipos;
    private readonly IProveedorDataSource _proveedores;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly MateriaPrimaService _materiaPrima;
    private readonly RecepcionesMateriaPrimaService _servicio;

    private string _filtro = FiltroTodas;

    public RecepcionesMateriaPrimaViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearRecepcionesMateriaPrima(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private RecepcionesMateriaPrimaViewModel(Modulo modulo,
                                             Submodulo submodulo,
                                             IRecepcionMateriaPrimaDataSource recepciones,
                                             IServicioDialogo dialogos,
                                             ISesionActual sesion)
        : base(modulo, submodulo, recepciones, dialogos, sesion)
    {
        _dialogos = dialogos;
        _sesionActual = sesion;
        _tipos = DataSourceFactory.CrearTiposMateriaPrima();
        _proveedores = DataSourceFactory.CrearProveedores();

        var facturas = DataSourceFactory.CrearFacturasProveedor();

        _materiaPrima = new MateriaPrimaService(_tipos, recepciones, DataSourceFactory.CrearSalidasMateriaPrima());

        // La misma cadena de dependencias que arma Cuentas por Pagar, más un eslabón: la
        // recepción necesita al servicio de Finanzas para dejar la deuda, y ése necesita al de
        // Banco —mismo criterio que EntradasViewModel.
        var banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                     DataSourceFactory.CrearCuentasBancarias(), sesion);

        _servicio = new RecepcionesMateriaPrimaService(recepciones, _proveedores, facturas, _materiaPrima,
                                                       new CuentasPorPagarService(facturas, banco, sesion),
                                                       sesion);

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } r && _servicio.PuedeAnular(r)
                  && _sesionActual.Puede(Permisos.RecepcionesMateriaPrima.Anular));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    public string Resumen =>
        $"{Items.Count(r => r.Estado == EstadoRecepcionMateriaPrima.Registrada)} recepciones · " +
        $"{_servicio.DelMes().Count} este mes · comprado este mes {_servicio.TotalComprasDelMes():N2}";

    protected override string ModuloPermiso => "RecepcionesMateriaPrima";

    protected override bool CoincideBusqueda(RecepcionMateriaPrima item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Referencia.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ProveedorNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroDocumento.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.RecibidoPor.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.TipoMateriaPrimaNombre.Contains(texto, StringComparison.OrdinalIgnoreCase));

    protected override bool PasaFiltroExtra(RecepcionMateriaPrima item) => _filtro switch
    {
        "Compras a proveedor" => item.Tipo == TipoRecepcionMateriaPrima.CompraProveedor,
        "Compras externas" => item.Tipo == TipoRecepcionMateriaPrima.CompraExterna,
        "Otro origen" => item.Tipo == TipoRecepcionMateriaPrima.OtroOrigen,
        "Anuladas" => item.Estado == EstadoRecepcionMateriaPrima.Anulada,
        _ => true
    };

    /// <summary>Un documento no se corrige ni se borra: se anula, y eso deja constancia.</summary>
    protected override bool PuedeEditar(RecepcionMateriaPrima item) => false;

    protected override bool PuedeEliminar(RecepcionMateriaPrima item) => false;

    protected override RecepcionMateriaPrima CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        FechaVencimiento = DateTime.Today.AddDays(30),
        Estado = EstadoRecepcionMateriaPrima.Registrada
    };

    protected override CrudEditorViewModelBase<RecepcionMateriaPrima> CrearEditor(RecepcionMateriaPrima item) =>
        new RecepcionMateriaPrimaEditorViewModel(item,
                                                 [.. _proveedores.GetAll().Where(p => p.Activo).OrderBy(p => p.Nombre)],
                                                 [.. _tipos.GetAll().Where(t => t.Activo).OrderBy(t => t.Nombre)],
                                                 _servicio);

    /// <summary>
    /// El alta pasa por el servicio de dominio y no por la fuente de datos: registrar la
    /// recepción exige el correlativo y las validaciones de negocio, que el CRUD genérico no
    /// conoce.
    /// </summary>
    protected override void Agregar()
    {
        if (!_tipos.GetAll().Any(t => t.Activo))
        {
            _dialogos.Informar("No hay tipos de materia prima",
                "Antes de registrar una recepción, agregue al menos un tipo de materia prima en " +
                "Materia Prima · Existencias.");
            return;
        }

        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(),
                                          _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } recepcion)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular recepción {recepcion.Numero}",
            $"{recepcion.TipoTexto} — {recepcion.OrigenTexto} — {recepcion.TotalCantidad:N2} en total",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(recepcion, editor.Motivo));
    }

    private void Aplicar(Func<RecepcionMateriaPrima> transicion)
    {
        try
        {
            SeleccionarTrasRecargar(transicion().Id);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
    }
}

/// <summary>
/// El modal de "Registrar recepción": uno solo para las tres formas en que llega materia prima.
///
/// Son tres modos y no tres ventanas porque lo que cambia entre ellos es la cabecera —a quién se
/// le compró y con qué papel—, no el cuerpo: la lista de tipos que llegan es la misma en los tres.
/// Cambiar de modo conserva las líneas ya cargadas —mismo criterio que <see cref="EntradaEditorViewModel"/>.
/// </summary>
public sealed class RecepcionMateriaPrimaEditorViewModel : CrudEditorViewModelBase<RecepcionMateriaPrima>
{
    private readonly RecepcionMateriaPrima _original;
    private readonly RecepcionesMateriaPrimaService _servicio;

    public RecepcionMateriaPrimaEditorViewModel(RecepcionMateriaPrima original,
                                                IReadOnlyList<Proveedor> proveedores,
                                                IReadOnlyList<TipoMateriaPrima> tipos,
                                                RecepcionesMateriaPrimaService servicio)
    {
        _original = original;
        _servicio = servicio;

        Proveedores = proveedores;
        Tipos = tipos;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        FechaVencimiento = original.FechaVencimiento ?? DateTime.Today.AddDays(30);
        NumeroDocumento = original.NumeroDocumento;
        Comercio = original.Tipo == TipoRecepcionMateriaPrima.CompraExterna ? original.ProveedorNombre : string.Empty;
        RecibidoPor = original.RecibidoPor;
        Referencia = original.Referencia;
        Observaciones = original.Observaciones;

        Lineas.CollectionChanged += AlCambiarLineas;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaRecepcionMateriaPrimaEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    public override string Titulo => "Registrar recepción";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Registrar recepción";

    public IReadOnlyList<Proveedor> Proveedores { get; }
    public IReadOnlyList<TipoMateriaPrima> Tipos { get; }

    public ObservableCollection<LineaRecepcionMateriaPrimaEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

    // --- Modo ---

    private TipoRecepcionMateriaPrima _tipo = TipoRecepcionMateriaPrima.CompraProveedor;
    public TipoRecepcionMateriaPrima Tipo
    {
        get => _tipo;
        set
        {
            // Notifica todas: enumerar aquí un OnPropertyChanged por cada Muestra… es la lista
            // que se queda corta el día que se agrega un modo más.
            if (SetProperty(ref _tipo, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    /// <summary>
    /// Las tres pestañas, enlazadas en DOS VÍAS al <c>IsChecked</c> de su botón, igual que
    /// <see cref="EntradaEditorViewModel"/>. El setter solo actúa al marcar: al desmarcar ya hay
    /// otro botón del grupo encendiéndose.
    /// </summary>
    public bool EsCompraProveedor
    {
        get => Tipo == TipoRecepcionMateriaPrima.CompraProveedor;
        set { if (value) Tipo = TipoRecepcionMateriaPrima.CompraProveedor; }
    }

    public bool EsCompraExterna
    {
        get => Tipo == TipoRecepcionMateriaPrima.CompraExterna;
        set { if (value) Tipo = TipoRecepcionMateriaPrima.CompraExterna; }
    }

    public bool EsOtroOrigen
    {
        get => Tipo == TipoRecepcionMateriaPrima.OtroOrigen;
        set { if (value) Tipo = TipoRecepcionMateriaPrima.OtroOrigen; }
    }

    public bool MuestraProveedor => Tipo == TipoRecepcionMateriaPrima.CompraProveedor;
    public bool MuestraComercio => Tipo == TipoRecepcionMateriaPrima.CompraExterna;

    /// <summary>En otro origen no se compró nada, así que no hay precios que pedir.</summary>
    public bool MuestraPrecios => Tipo != TipoRecepcionMateriaPrima.OtroOrigen;

    public string NotaModo => Tipo switch
    {
        TipoRecepcionMateriaPrima.CompraProveedor =>
            "Al registrarla se creará la cuenta por pagar en Finanzas · Cuentas por Pagar.",
        TipoRecepcionMateriaPrima.CompraExterna =>
            "Si el proveedor no está en el padrón se dará de alta solo, y la cuenta por pagar " +
            "quedará a su nombre.",
        _ => "Un aporte, cosecha propia u otro origen sin compra no genera ninguna cuenta por pagar."
    };

    // --- Cabecera ---

    private Proveedor? _proveedorSeleccionado;
    public Proveedor? ProveedorSeleccionado
    {
        get => _proveedorSeleccionado;
        set => SetProperty(ref _proveedorSeleccionado, value);
    }

    private string _comercio = string.Empty;
    public string Comercio
    {
        get => _comercio;
        set => SetProperty(ref _comercio, value);
    }

    private string _recibidoPor = string.Empty;
    public string RecibidoPor
    {
        get => _recibidoPor;
        set => SetProperty(ref _recibidoPor, value);
    }

    private string _numeroDocumento = string.Empty;
    public string NumeroDocumento
    {
        get => _numeroDocumento;
        set => SetProperty(ref _numeroDocumento, value);
    }

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private DateTime? _fechaVencimiento;
    public DateTime? FechaVencimiento
    {
        get => _fechaVencimiento;
        set => SetProperty(ref _fechaVencimiento, value);
    }

    private string _referencia = string.Empty;
    public string Referencia
    {
        get => _referencia;
        set => SetProperty(ref _referencia, value);
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    // --- Total ---

    public decimal TotalCantidad => Lineas.Sum(l => l.CantidadValor);

    public decimal Total => Lineas.Sum(l => l.Subtotal);

    public string TotalTexto => Total.ToString("N2");

    // --- Validación y resultado ---

    protected override bool Validar(out string? error)
    {
        if (Lineas.Any(l => l.TipoSeleccionado is not null && !l.CantidadEsValida))
        {
            error = "Hay una cantidad que no es un número válido.";
            return false;
        }

        if (MuestraPrecios && Lineas.Any(l => l.TipoSeleccionado is not null && !l.PrecioEsValido))
        {
            error = "Hay un precio que no es un número válido.";
            return false;
        }

        // La autoridad es el servicio: aquí solo se le pregunta, para que el mensaje salga en el
        // formulario en vez de en un diálogo de error después de cerrarlo.
        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override RecepcionMateriaPrima ObtenerResultado()
    {
        var recepcion = _original.Clonar();
        recepcion.Tipo = Tipo;
        recepcion.Fecha = Fecha.Date;
        recepcion.Referencia = Referencia.Trim();
        recepcion.Observaciones = Observaciones.Trim();

        recepcion.ProveedorId = Tipo == TipoRecepcionMateriaPrima.CompraProveedor ? ProveedorSeleccionado?.Id : null;
        recepcion.ProveedorNombre = Tipo switch
        {
            TipoRecepcionMateriaPrima.CompraProveedor => ProveedorSeleccionado?.Nombre ?? string.Empty,
            TipoRecepcionMateriaPrima.CompraExterna => Comercio.Trim(),
            _ => string.Empty
        };

        recepcion.RecibidoPor = Tipo == TipoRecepcionMateriaPrima.CompraExterna ? RecibidoPor.Trim() : string.Empty;
        recepcion.NumeroDocumento = MuestraPrecios ? NumeroDocumento.Trim() : string.Empty;
        recepcion.FechaVencimiento = MuestraPrecios ? FechaVencimiento?.Date : null;

        // Las líneas en blanco no se mandan: una fila vacía al final es lo normal mientras se
        // carga la recepción, y no tiene por qué impedir guardar.
        recepcion.Lineas = [.. Lineas
            .Where(l => l.TipoSeleccionado is not null)
            .Select(l => l.Construir(MuestraPrecios))];

        recepcion.Total = recepcion.Lineas.Sum(l => l.Subtotal);
        return recepcion;
    }

    private LineaRecepcionMateriaPrimaEditorViewModel NuevaLinea()
    {
        var linea = new LineaRecepcionMateriaPrimaEditorViewModel(Tipos);
        linea.Cambio += AlCambiarLinea;
        return linea;
    }

    private void AlCambiarLineas(object? remitente, NotifyCollectionChangedEventArgs e)
    {
        foreach (var linea in e.OldItems?.OfType<LineaRecepcionMateriaPrimaEditorViewModel>() ?? [])
            linea.Cambio -= AlCambiarLinea;

        AlCambiarLinea(this, EventArgs.Empty);
    }

    private void AlCambiarLinea(object? remitente, EventArgs e)
    {
        OnPropertyChanged(nameof(TotalCantidad));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalTexto));
    }
}

/// <summary>
/// Un renglón de la grilla de líneas mientras se está escribiendo.
///
/// Es un ViewModel y no el modelo <see cref="RecepcionMateriaPrimaLinea"/> porque los modelos no
/// avisan de sus cambios, y aquí hace falta: el subtotal y el total del pie tienen que moverse
/// según se teclea. Las cantidades viajan como texto por el mismo motivo que en el resto de los
/// editores: un campo vacío no es un cero.
/// </summary>
public sealed class LineaRecepcionMateriaPrimaEditorViewModel : ViewModelBase
{
    /// <summary>Avisa al editor de que hay que recalcular el total.</summary>
    public event EventHandler? Cambio;

    public LineaRecepcionMateriaPrimaEditorViewModel(IReadOnlyList<TipoMateriaPrima> tipos)
    {
        Tipos = tipos;
    }

    public IReadOnlyList<TipoMateriaPrima> Tipos { get; }

    private TipoMateriaPrima? _tipoSeleccionado;
    public TipoMateriaPrima? TipoSeleccionado
    {
        get => _tipoSeleccionado;
        set { if (SetProperty(ref _tipoSeleccionado, value)) Recalcular(); }
    }

    private string _cantidad = string.Empty;
    public string Cantidad
    {
        get => _cantidad;
        set { if (SetProperty(ref _cantidad, value)) Recalcular(); }
    }

    private string _precioUnitario = string.Empty;
    public string PrecioUnitario
    {
        get => _precioUnitario;
        set { if (SetProperty(ref _precioUnitario, value)) Recalcular(); }
    }

    public bool CantidadEsValida =>
        !string.IsNullOrWhiteSpace(Cantidad) && decimal.TryParse(Cantidad, out var v) && v > 0;

    public bool PrecioEsValido =>
        !string.IsNullOrWhiteSpace(PrecioUnitario) && decimal.TryParse(PrecioUnitario, out var v) && v > 0;

    public decimal CantidadValor => decimal.TryParse(Cantidad, out var valor) ? valor : 0;

    public decimal PrecioValor => decimal.TryParse(PrecioUnitario, out var valor) ? valor : 0m;

    public decimal Subtotal => CantidadValor * PrecioValor;

    public string SubtotalTexto => Subtotal.ToString("N2");

    /// <summary>Lo que hay hoy en existencia de ese tipo; se muestra como referencia.</summary>
    public string ExistenciaTexto => TipoSeleccionado is { } tipo
        ? $"{tipo.Existencia:N2} {tipo.UnidadMedida}".Trim()
        : string.Empty;

    public RecepcionMateriaPrimaLinea Construir(bool conPrecios) => new()
    {
        TipoMateriaPrimaId = TipoSeleccionado!.Id,
        TipoMateriaPrimaNombre = TipoSeleccionado.Nombre,
        UnidadMedidaSnapshot = TipoSeleccionado.UnidadMedida,
        Cantidad = CantidadValor,
        PrecioUnitario = conPrecios ? PrecioValor : 0m,
        Subtotal = conPrecios ? Subtotal : 0m
    };

    private void Recalcular()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalTexto));
        OnPropertyChanged(nameof(ExistenciaTexto));
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}
