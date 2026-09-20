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
/// Inventario · Entradas: el historial de lo que llegó al almacén y el registro de lo nuevo.
///
/// Las filas son documentos: no se editan ni se borran, se anulan. Por eso la pantalla apaga
/// Editar y Eliminar y solo deja "Registrar entrada" y "Anular".
/// </summary>
public sealed class EntradasViewModel : PantallaCrudViewModel<EntradaInventario, int>
{
    private const string FiltroTodas = "Todas";

    private readonly IProveedorDataSource _proveedores;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly InventarioService _inventario;
    private readonly EntradasInventarioService _servicio;

    private string _filtro = FiltroTodas;

    public EntradasViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearEntradasInventario(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private EntradasViewModel(Modulo modulo,
                              Submodulo submodulo,
                              IEntradaInventarioDataSource entradas,
                              IServicioDialogo dialogos,
                              ISesionActual sesion)
        : base(modulo, submodulo, entradas, dialogos, sesion)
    {
        _dialogos = dialogos;
        _sesionActual = sesion;
        _proveedores = DataSourceFactory.CrearProveedores();

        var articulos = DataSourceFactory.CrearArticulos();
        var facturas = DataSourceFactory.CrearFacturasProveedor();

        _inventario = new InventarioService(articulos, entradas,
                                            DataSourceFactory.CrearSalidasInventario());

        // La misma cadena de dependencias que arma Cuentas por Pagar, más un eslabón: la entrada
        // necesita al servicio de Finanzas para dejar la deuda, y ése necesita al de Banco.
        var banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                     DataSourceFactory.CrearCuentasBancarias(), sesion);

        _servicio = new EntradasInventarioService(entradas, _proveedores, facturas, _inventario,
                                                  new CuentasPorPagarService(facturas, banco, sesion),
                                                  sesion);

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } e && _servicio.PuedeAnular(e)
                  && _sesionActual.Puede(Permisos.EntradasInventario.Anular));

        VerDetalleCommand = new RelayCommand(VerDetalle);

        ImprimirCommand = new RelayCommand(Imprimir, () => SelectedItem is not null);

        VerFacturaCommand = new RelayCommand(VerFactura,
            () => SelectedItem?.FacturaProveedorId is not null);
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla (ver <c>EntradasView.xaml.cs</c>), nunca
    /// un botón — por eso no lleva <c>CanExecute</c>: la guarda de "hay algo seleccionado" vive
    /// dentro de <see cref="VerDetalle"/>. Es una consulta de solo lectura, así que funciona igual
    /// sin importar el <see cref="EstadoEntrada"/> de la entrada.</summary>
    public ICommand VerDetalleCommand { get; }

    /// <summary>Saca el documento por la impresora. Ver <see cref="Services.ImpresionDocumento"/>.</summary>
    public ICommand ImprimirCommand { get; }

    /// <summary>Abre la cuenta por pagar que generó esta entrada, con la fila ya marcada.</summary>
    public ICommand VerFacturaCommand { get; }

    public string Resumen =>
        $"{Items.Count(e => e.Estado == EstadoEntrada.Registrada)} entradas · " +
        $"comprado este mes {_servicio.TotalComprasDelMes():N2}";

    protected override string ModuloPermiso => "EntradasInventario";

    protected override string Describir(EntradaInventario item) => item.Numero;

    protected override string NombreDelTipo => "Entrada";

    protected override bool CoincideBusqueda(EntradaInventario item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ProveedorNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroDocumento.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.CompradoPor.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.ArticuloNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
                                || l.ArticuloCodigo.Contains(texto, StringComparison.OrdinalIgnoreCase));

    protected override bool PasaFiltroExtra(EntradaInventario item) => _filtro switch
    {
        "Compras a proveedor" => item.Tipo == TipoEntrada.CompraProveedor,
        "Compras externas" => item.Tipo == TipoEntrada.CompraExterna,
        "Ajustes" => item.Tipo == TipoEntrada.Ajuste,
        "Anuladas" => item.Estado == EstadoEntrada.Anulada,
        _ => true
    };

    /// <summary>Un documento no se corrige ni se borra: se anula, y eso deja constancia.</summary>
    protected override bool PuedeEditar(EntradaInventario item) => false;

    protected override bool PuedeEliminar(EntradaInventario item) => false;

    protected override EntradaInventario CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        FechaVencimiento = DateTime.Today.AddDays(30),
        Estado = EstadoEntrada.Registrada
    };

    protected override CrudEditorViewModelBase<EntradaInventario> CrearEditor(EntradaInventario item) =>
        new EntradaEditorViewModel(item,
                                   [.. _proveedores.GetAll().Where(p => p.Activo).OrderBy(p => p.Nombre)],
                                   _inventario.ActivosConExistencia(),
                                   _servicio,
                                   _inventario,
                                   _dialogos,
                                   _sesionActual);

    /// <summary>
    /// El alta pasa por el servicio de dominio y no por la fuente de datos: registrar la entrada
    /// es también dejar la cuenta por pagar en Finanzas, y eso no lo sabe el CRUD genérico.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(),
                                          _sesionActual.UsuarioActual?.Id ?? 0),
                e => $"Entrada {e.Numero} registrada");
    }

    private void Anular()
    {
        if (SelectedItem is not { } entrada)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular entrada {entrada.Numero}",
            $"{entrada.TipoTexto} — {entrada.OrigenTexto} — {entrada.TotalTexto}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(entrada, editor.Motivo),
                e => $"Entrada {e.Numero} anulada");
    }

    /// <summary>
    /// Salta a la cuenta por pagar que generó esta entrada.
    ///
    /// El enlace ya estaba guardado en el documento (<c>EntradaInventario.FacturaProveedorId</c>) y la ficha lo
    /// pintaba como texto: se veia el numero de la factura pero llegar a ella era volver al
    /// menu, entrar al submodulo y buscarla a mano.
    /// </summary>
    private void VerFactura()
    {
        if (SelectedItem?.FacturaProveedorId is not { } facturaId)
            return;

        if (ModuloCatalogo.BuscarModulo("Finanzas") is { } destino
            && ModuloCatalogo.BuscarSubmodulo("Finanzas.CuentasPorPagar") is { } seccion)
            SolicitarNavegacion(destino, seccion, facturaId);
    }

    /// <summary>
    /// Imprime la fila seleccionada. El documento no se vuelve a leer de la base: se imprime
    /// EXACTAMENTE lo que está en pantalla, porque el papel que se entrega tiene que
    /// coincidir con lo que vio quien lo emitió.
    /// </summary>
    private void Imprimir()
    {
        if (SelectedItem is not { } documento)
            return;

        if (ImpresionDocumento.Imprimir(DocumentosImprimibles.De(documento)))
            Aviso.Mostrar($"Enviado a la impresora: {documento.Numero}");
    }

    private void VerDetalle()
    {
        if (SelectedItem is not { } entrada)
            return;

        _dialogos.MostrarEditor(new EntradaDetalleViewModel(entrada));
    }

    /// <summary>
    /// La lista la repuebla la recarga que dispara la escritura del servicio; aquí solo se apunta
    /// qué entrada dejar seleccionada.
    /// </summary>
    private void Aplicar(Func<EntradaInventario> transicion, Func<EntradaInventario, string> aviso)
    {
        try
        {
            var resultado = transicion();
            SeleccionarTrasRecargar(resultado.Id);
            Aviso.Mostrar(aviso(resultado));
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
        catch (Exception ex)
        {
            // Lo que NO es una regla de negocio —la conexión que se cae, la escritura que choca
            // con un índice— subía sin capturar hasta el despachador y cerraba la aplicación a
            // media operación. Va en un catch aparte a propósito: el mensaje de arriba lo redactó
            // el servicio para quien lo lee, este es técnico y no se puede prometer más.
            _dialogos.Informar("No se pudo guardar",
                "La operación no llegó a completarse. " + ex.Message);
        }
    }
}

/// <summary>
/// La ficha de "ver detalle" de una entrada: solo lectura, se abre con doble clic sobre la fila
/// (ver <c>EntradasView.xaml.cs</c>). Expone la <see cref="EntradaInventario"/> completa, mismo
/// criterio que <see cref="ProcesoDetalleViewModel"/>.
/// </summary>
public sealed class EntradaDetalleViewModel : CrudEditorViewModelBase
{
    public EntradaDetalleViewModel(EntradaInventario entrada)
    {
        Entrada = entrada;
    }

    public EntradaInventario Entrada { get; }

    public override string Titulo => $"Entrada {Entrada.Numero}";

    /// <summary>Amplio: lleva la grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Cerrar";

    public override bool MuestraCancelar => false;

    protected override bool Validar(out string? error)
    {
        error = null;
        return true;
    }
}

/// <summary>
/// El modal de "Registrar entrada": uno solo para las tres formas en que algo llega al almacén.
///
/// Son tres modos y no tres ventanas porque lo que cambia entre ellos es la cabecera —a quién se
/// le compró y con qué papel—, no el cuerpo: la lista de artículos que entran es la misma en los
/// tres. Cambiar de modo conserva las líneas ya cargadas.
/// </summary>
public sealed class EntradaEditorViewModel : CrudEditorViewModelBase<EntradaInventario>
{
    private readonly EntradaInventario _original;
    private readonly EntradasInventarioService _servicio;

    public EntradaEditorViewModel(EntradaInventario original,
                                  IReadOnlyList<Proveedor> proveedores,
                                  IReadOnlyList<Articulo> articulos,
                                  EntradasInventarioService servicio,
                                  InventarioService inventario,
                                  IServicioDialogo dialogos,
                                  ISesionActual sesion)
    {
        _original = original;
        _servicio = servicio;

        Proveedores = proveedores;
        Articulos = new ObservableCollection<Articulo>(articulos);

        NuevoArticuloCommand = new RelayCommand<LineaEntradaEditorViewModel>(linea =>
        {
            var editor = new ArticuloEditorViewModel(new Articulo { Activo = true }, inventario);
            if (!dialogos.MostrarEditor(editor))
                return;

            var nuevo = DataSourceFactory.CrearArticulos().Add(editor.ObtenerResultado());
            Articulos.Add(nuevo);

            if (linea is not null)
                linea.ArticuloSeleccionado = nuevo;
        },
        _ => sesion.Puede(Permisos.Articulos.Crear));

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        FechaVencimiento = original.FechaVencimiento ?? DateTime.Today.AddDays(30);
        NumeroDocumento = original.NumeroDocumento;
        Comercio = original.Tipo == TipoEntrada.CompraExterna ? original.ProveedorNombre : string.Empty;
        CompradoPor = original.CompradoPor;
        Observaciones = original.Observaciones;

        Lineas.CollectionChanged += AlCambiarLineas;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaEntradaEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    public override string Titulo => "Registrar entrada";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Registrar entrada";

    public IReadOnlyList<Proveedor> Proveedores { get; }
    public ObservableCollection<Articulo> Articulos { get; }

    public ObservableCollection<LineaEntradaEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }
    public ICommand NuevoArticuloCommand { get; }

    // --- Modo ---

    private TipoEntrada _tipo = TipoEntrada.CompraProveedor;
    public TipoEntrada Tipo
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
    /// Las tres pestañas, enlazadas en DOS VÍAS al <c>IsChecked</c> de su botón, igual que el
    /// conmutador de Cuentas por Pagar. El setter solo actúa al marcar: al desmarcar ya hay otro
    /// botón del grupo encendiéndose.
    /// </summary>
    public bool EsCompraProveedor
    {
        get => Tipo == TipoEntrada.CompraProveedor;
        set { if (value) Tipo = TipoEntrada.CompraProveedor; }
    }

    public bool EsCompraExterna
    {
        get => Tipo == TipoEntrada.CompraExterna;
        set { if (value) Tipo = TipoEntrada.CompraExterna; }
    }

    public bool EsAjuste
    {
        get => Tipo == TipoEntrada.Ajuste;
        set { if (value) Tipo = TipoEntrada.Ajuste; }
    }

    public bool MuestraProveedor => Tipo == TipoEntrada.CompraProveedor;
    public bool MuestraComercio => Tipo == TipoEntrada.CompraExterna;

    /// <summary>En un ajuste no se compró nada, así que no hay precios que pedir.</summary>
    public bool MuestraPrecios => Tipo != TipoEntrada.Ajuste;

    public string NotaModo => Tipo switch
    {
        TipoEntrada.CompraProveedor =>
            "Al registrarla se creará la cuenta por pagar en Finanzas · Cuentas por Pagar.",
        TipoEntrada.CompraExterna =>
            "Si el comercio no está en el padrón de proveedores se dará de alta solo, y la cuenta " +
            "por pagar quedará a su nombre.",
        _ => "Un ajuste solo corrige la existencia: no genera ninguna cuenta por pagar."
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

    private string _compradoPor = string.Empty;
    public string CompradoPor
    {
        get => _compradoPor;
        set => SetProperty(ref _compradoPor, value);
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

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    // --- Total ---

    public decimal Total => Lineas.Sum(l => l.Subtotal);

    public string TotalTexto => Total.ToString("N2");

    // --- Validación y resultado ---

    protected override bool Validar(out string? error)
    {
        if (Lineas.Any(l => l.ArticuloSeleccionado is not null && !l.CantidadEsValida))
        {
            error = "Hay una cantidad que no es un número válido.";
            return false;
        }

        if (MuestraPrecios && Lineas.Any(l => l.ArticuloSeleccionado is not null && !l.PrecioEsValido))
        {
            error = "Hay un precio que no es un número válido.";
            return false;
        }

        // La autoridad es el servicio: aquí solo se le pregunta, para que el mensaje salga en el
        // formulario en vez de en un diálogo de error después de cerrarlo.
        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override EntradaInventario ObtenerResultado()
    {
        var entrada = _original.Clonar();
        entrada.Tipo = Tipo;
        entrada.Fecha = Fecha.Date;
        entrada.Observaciones = Observaciones.Trim();

        entrada.ProveedorId = Tipo == TipoEntrada.CompraProveedor ? ProveedorSeleccionado?.Id : null;
        entrada.ProveedorNombre = Tipo switch
        {
            TipoEntrada.CompraProveedor => ProveedorSeleccionado?.Nombre ?? string.Empty,
            TipoEntrada.CompraExterna => Comercio.Trim(),
            _ => string.Empty
        };

        entrada.CompradoPor = Tipo == TipoEntrada.CompraExterna ? CompradoPor.Trim() : string.Empty;
        entrada.NumeroDocumento = MuestraPrecios ? NumeroDocumento.Trim() : string.Empty;
        entrada.FechaVencimiento = MuestraPrecios ? FechaVencimiento?.Date : null;

        // Las líneas en blanco no se mandan: una fila vacía al final es lo normal mientras se
        // carga la entrada, y no tiene por qué impedir guardar.
        entrada.Lineas = [.. Lineas
            .Where(l => l.ArticuloSeleccionado is not null)
            .Select(l => l.Construir(MuestraPrecios))];

        entrada.Total = entrada.Lineas.Sum(l => l.Subtotal);
        return entrada;
    }

    // --- Líneas ---

    private LineaEntradaEditorViewModel NuevaLinea()
    {
        var linea = new LineaEntradaEditorViewModel(Articulos);
        linea.Cambio += AlCambiarLinea;
        return linea;
    }

    private void AlCambiarLineas(object? remitente, NotifyCollectionChangedEventArgs e)
    {
        foreach (var linea in e.OldItems?.OfType<LineaEntradaEditorViewModel>() ?? [])
            linea.Cambio -= AlCambiarLinea;

        AlCambiarLinea(this, EventArgs.Empty);
    }

    private void AlCambiarLinea(object? remitente, EventArgs e)
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalTexto));
    }
}

/// <summary>
/// Un renglón de la grilla de líneas mientras se está escribiendo.
///
/// Es un ViewModel y no el modelo <see cref="EntradaInventarioLinea"/> porque los modelos no
/// avisan de sus cambios, y aquí hace falta: el subtotal y el total del pie tienen que moverse
/// según se teclea. Las cantidades viajan como texto por el mismo motivo que en el resto de los
/// editores: un campo vacío no es un cero.
/// </summary>
public sealed class LineaEntradaEditorViewModel : ViewModelBase
{
    /// <summary>Avisa al editor de que hay que recalcular el total.</summary>
    public event EventHandler? Cambio;

    public LineaEntradaEditorViewModel(IReadOnlyList<Articulo> articulos)
    {
        Articulos = articulos;
    }

    public IReadOnlyList<Articulo> Articulos { get; }

    private Articulo? _articuloSeleccionado;
    public Articulo? ArticuloSeleccionado
    {
        get => _articuloSeleccionado;
        set { if (SetProperty(ref _articuloSeleccionado, value)) Recalcular(); }
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

    public decimal CantidadValor => decimal.TryParse(Cantidad, out var valor) ? valor : 0m;

    public decimal PrecioValor => decimal.TryParse(PrecioUnitario, out var valor) ? valor : 0m;

    public decimal Subtotal => CantidadValor * PrecioValor;

    public string SubtotalTexto => Subtotal.ToString("N2");

    public string UnidadTexto => ArticuloSeleccionado?.UnidadMedida ?? string.Empty;

    /// <summary>Lo que hay hoy en el almacén de ese artículo; se muestra como referencia.</summary>
    public string ExistenciaTexto => ArticuloSeleccionado is { } articulo
        ? $"{articulo.Existencia:N2} {articulo.UnidadMedida}"
        : string.Empty;

    public EntradaInventarioLinea Construir(bool conPrecios) => new()
    {
        ArticuloId = ArticuloSeleccionado!.Id,
        ArticuloCodigo = ArticuloSeleccionado.Codigo,
        ArticuloNombre = ArticuloSeleccionado.Nombre,
        UnidadTexto = ArticuloSeleccionado.UnidadMedida,
        Cantidad = CantidadValor,
        PrecioUnitario = conPrecios ? PrecioValor : 0m,
        Subtotal = conPrecios ? Subtotal : 0m
    };

    private void Recalcular()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalTexto));
        OnPropertyChanged(nameof(UnidadTexto));
        OnPropertyChanged(nameof(ExistenciaTexto));
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}
