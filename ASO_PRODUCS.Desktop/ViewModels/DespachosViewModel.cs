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
/// Procesos · Despacho: el catálogo de productos terminados con su existencia al día.
///
/// Mismo arquetipo que <see cref="ExistenciasMateriaPrimaViewModel"/>: es el maestro CRUD del
/// módulo (aquí se dan de alta los productos) con una vuelta — la existencia no está en la
/// tabla, sale de lo que produjeron los procesos Terminados menos lo despachado. Como los
/// modelos no avisan de sus cambios, cada vez que se rellena hay que refrescar la vista a mano.
/// </summary>
public sealed class ProductosCrudViewModel : CrudViewModelBase<Producto, int>
{
    private const string FiltroTodos = "Todos";

    private readonly ProductosService _servicio;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    private string _filtro = FiltroTodos;

    public ProductosCrudViewModel(IProductoDataSource productos,
                                  ProductosService servicio,
                                  IServicioDialogo dialogos,
                                  ISesionActual sesion)
        : base(productos, dialogos, sesion)
    {
        _servicio = servicio;
        _dialogos = dialogos;
        _sesion = sesion;

        // La base ya pobló Items en su constructor, pero sin existencias: sin esto la primera
        // pintada saldría con todo en cero hasta la primera recarga.
        _servicio.RellenarExistencias(Items);
        ItemsView.Refresh();

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        CargarSugeridosCommand = new RelayCommand(CargarSugeridos, () => _sesion.Puede($"{ModuloPermiso}.Crear"));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand CargarSugeridosCommand { get; }

    public string Resumen =>
        $"{_servicio.TotalProductosActivos()} productos activos · {_servicio.ProductosSinExistencia()} sin existencia";

    protected override string ModuloPermiso => "Productos";

    protected override bool CoincideBusqueda(Producto item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(Producto item) => _filtro switch
    {
        "Con existencia" => item.Existencia > 0,
        "Sin existencia" => item.SinExistencia,
        "Inactivos" => !item.Activo,
        _ => true
    };

    /// <summary>Un producto que ya se produjo o se despachó no se borra, se desactiva.</summary>
    protected override bool PuedeEliminar(Producto item) => _servicio.PuedeEliminar(item);

    protected override Producto CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Producto> CrearEditor(Producto item) =>
        new ProductoEditorViewModel(item, _servicio);

    /// <summary>Relee el catálogo y le vuelve a pegar la existencia encima. El refresco va
    /// después de rellenar: el filtro "sin existencia" depende justamente de ese número.</summary>
    public override void Recargar()
    {
        base.Recargar();

        _servicio.RellenarExistencias(Items);
        ItemsView.Refresh();
        OnTodasLasPropiedadesCambiaron();
    }

    /// <summary>Precarga el catálogo sugerido para una planta láctea; no duplica lo que ya
    /// exista, así que se puede pulsar más de una vez sin riesgo.</summary>
    private void CargarSugeridos()
    {
        var creados = _servicio.CargarSugeridos(CatalogoLacteoSugerido.Productos);
        Recargar();

        _dialogos.Informar("Catálogo cargado",
            creados > 0
                ? $"Se agregaron {creados} productos sugeridos."
                : "Los productos sugeridos ya estaban todos cargados.");
    }
}

/// <summary>Alta/edición de un producto terminado.</summary>
public sealed class ProductoEditorViewModel : CrudEditorViewModelBase<Producto>
{
    private readonly Producto _original;
    private readonly ProductosService _servicio;

    public ProductoEditorViewModel(Producto original, ProductosService servicio)
    {
        _original = original;
        _servicio = servicio;

        Nombre = original.Nombre;
        UnidadMedida = original.UnidadMedida;
        Precio = original.PrecioUnitario > 0 ? original.PrecioUnitario.ToString("0.####") : string.Empty;
        Activo = original.Id == 0 || original.Activo;
    }

    public override string Titulo => _original.Id == 0 ? "Nuevo producto" : $"Editar {_original.Nombre}";

    public override double AnchoEditor => Ancho.Estandar;

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _unidadMedida = string.Empty;
    public string UnidadMedida
    {
        get => _unidadMedida;
        set => SetProperty(ref _unidadMedida, value);
    }

    /// <summary>Precio de referencia por unidad; opcional (vacío/0 = sin precio configurado, el
    /// despacho lo sigue pidiendo a mano en ese caso). La validación de que no sea negativo la hace
    /// <see cref="ProductosService.Validar"/>, no acá.</summary>
    private string _precio = string.Empty;
    public string Precio
    {
        get => _precio;
        set => SetProperty(ref _precio, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error) => _servicio.Validar(ObtenerResultado(), out error);

    public override Producto ObtenerResultado()
    {
        var producto = _original.Clonar();
        producto.Nombre = Nombre.Trim();
        producto.UnidadMedida = UnidadMedida.Trim();
        producto.PrecioUnitario = decimal.TryParse(Precio, out var precio) ? precio : 0m;
        producto.Activo = Activo;
        return producto;
    }
}

/// <summary>
/// Procesos · Despacho: el historial de lo que salió y el registro de lo nuevo.
///
/// Misma forma que Materia Prima · Salidas: las filas son documentos, así que no se editan ni se
/// borran, se anulan. Anular devuelve la existencia sola, porque el kardex de productos ignora
/// lo anulado. Desde que un despacho de tipo Venta acopla con Finanzas, necesita además el
/// padrón de clientes para ofrecerlo en el editor.
/// </summary>
public sealed class DespachosCrudViewModel : CrudViewModelBase<Despacho, int>
{
    private const string FiltroTodos = "Todos";

    private readonly ProductosService _productos;
    private readonly IReadOnlyList<Cliente> _clientes;
    private readonly IReadOnlyList<ProcesoProduccion> _procesosTerminados;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly DespachosService _servicio;

    private string _filtro = FiltroTodos;

    public DespachosCrudViewModel(IDespachoDataSource despachos,
                                  ProductosService productos,
                                  IClienteDataSource clientes,
                                  IProcesoProduccionDataSource procesos,
                                  DespachosService servicio,
                                  IServicioDialogo dialogos,
                                  ISesionActual sesion)
        : base(despachos, dialogos, sesion)
    {
        _productos = productos;
        _clientes = [.. clientes.GetAll().Where(c => c.Activo).OrderBy(c => c.Nombre)];
        // Solo procesos Terminado: son los únicos que de verdad produjeron algo que se pueda
        // haber despachado. El vínculo es informativo, así que no hace falta filtrar además por
        // si ya se "agotó" — no hay manejo de lotes.
        _procesosTerminados = [.. procesos.GetAll()
            .Where(p => p.Estado == EstadoProcesoProduccion.Terminado)
            .OrderByDescending(p => p.Fecha)];
        _servicio = servicio;
        _dialogos = dialogos;
        _sesionActual = sesion;

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } d && _servicio.PuedeAnular(d) && _sesionActual.Puede(Permisos.Despachos.Anular));

        VerDetalleCommand = new RelayCommand(VerDetalle);
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla (ver <c>DespachosView.xaml.cs</c>), nunca
    /// un botón — por eso no lleva <c>CanExecute</c>: la guarda de "hay algo seleccionado" vive
    /// dentro de <see cref="VerDetalle"/>. Es una consulta de solo lectura, así que funciona igual
    /// sin importar el <see cref="EstadoDespacho"/> del despacho.</summary>
    public ICommand VerDetalleCommand { get; }

    public string Resumen =>
        $"{Items.Count(d => d.Estado == EstadoDespacho.Registrado)} despachos · {_servicio.DelMes().Count} este mes";

    protected override string ModuloPermiso => "Despachos";

    protected override bool CoincideBusqueda(Despacho item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.AutorizadoPorNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ClienteNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.ProductoNombre.Contains(texto, StringComparison.OrdinalIgnoreCase));

    protected override bool PasaFiltroExtra(Despacho item) => _filtro switch
    {
        "Ventas" => item.Tipo == TipoDespacho.Venta,
        "Ajustes" => item.Tipo == TipoDespacho.Ajuste,
        "Anulados" => item.Estado == EstadoDespacho.Anulado,
        _ => true
    };

    protected override bool PuedeEditar(Despacho item) => false;

    protected override bool PuedeEliminar(Despacho item) => false;

    protected override Despacho CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Estado = EstadoDespacho.Registrado
    };

    protected override CrudEditorViewModelBase<Despacho> CrearEditor(Despacho item) =>
        new DespachoEditorViewModel(item, _productos.ActivosConExistencia(), _clientes, _procesosTerminados,
                                    _sesionActual.UsuarioActual?.NombreCompleto ?? string.Empty, _servicio);

    /// <summary>
    /// La emisión pasa por el servicio de dominio: es él quien asigna el número del despacho,
    /// estampa quién autoriza, comprueba que haya existencia suficiente EN VIVO y, si es una
    /// Venta, deja la cuenta por cobrar en Finanzas.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(), _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } despacho)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular despacho {despacho.Numero}",
            $"{despacho.TipoTexto} — {despacho.TotalCantidad:N2} en total — autorizó {despacho.AutorizadoPorNombre}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(despacho, editor.Motivo));
    }

    private void VerDetalle()
    {
        if (SelectedItem is not { } despacho)
            return;

        _dialogos.MostrarEditor(new DespachoDetalleViewModel(despacho));
    }

    private void Aplicar(Func<Despacho> transicion)
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
/// La ficha de "ver detalle" de un despacho: solo lectura, se abre con doble clic sobre la fila
/// (ver <c>DespachosView.xaml.cs</c>). Expone el <see cref="Despacho"/> completo, mismo criterio
/// que <see cref="ProcesoDetalleViewModel"/>.
/// </summary>
public sealed class DespachoDetalleViewModel : CrudEditorViewModelBase
{
    public DespachoDetalleViewModel(Despacho despacho)
    {
        Despacho = despacho;
    }

    public Despacho Despacho { get; }

    public override string Titulo => $"Despacho {Despacho.Numero}";

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
/// El despacho. "Autoriza" no es un campo: se muestra de solo lectura y lo estampa el servicio
/// con el usuario de la sesión, para que no se pueda escribir otro nombre en el papel.
///
/// Un solo editor conmutable para las dos formas en que sale producto terminado — Venta y Ajuste
/// — mismo patrón que <see cref="EntradaEditorViewModel"/>: cambiar de modo oculta/muestra el
/// cliente y las columnas de precio sin perder las líneas ya cargadas.
/// </summary>
public sealed class DespachoEditorViewModel : CrudEditorViewModelBase<Despacho>
{
    private readonly Despacho _original;
    private readonly IReadOnlyDictionary<int, decimal> _existencias;
    private readonly DespachosService _servicio;

    private readonly IReadOnlyList<ProcesoProduccion> _procesosTerminados;

    public DespachoEditorViewModel(Despacho original,
                                   IReadOnlyList<Producto> productos,
                                   IReadOnlyList<Cliente> clientes,
                                   IReadOnlyList<ProcesoProduccion> procesosTerminados,
                                   string autorizadoPorNombre,
                                   DespachosService servicio)
    {
        _original = original;
        _servicio = servicio;
        _procesosTerminados = procesosTerminados;

        // Los productos ya llegan con su existencia rellena (ActivosConExistencia), así que el
        // disponible de cada línea sale de esa misma lista sin otra consulta.
        _existencias = productos.ToDictionary(p => p.Id, p => p.Existencia);

        Productos = productos;
        Clientes = clientes;
        AutorizadoPorNombre = autorizadoPorNombre;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        Observaciones = original.Observaciones;
        _tipo = original.Id == 0 ? TipoDespacho.Venta : original.Tipo;
        ClienteSeleccionado = Clientes.FirstOrDefault(c => c.Id == original.ClienteId);

        Lineas.CollectionChanged += AlCambiarLineas;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaDespachoEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    public override string Titulo => "Registrar despacho";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Registrar despacho";

    public IReadOnlyList<Producto> Productos { get; }
    public IReadOnlyList<Cliente> Clientes { get; }

    /// <summary>Quién autoriza: el usuario de la sesión, de solo lectura.</summary>
    public string AutorizadoPorNombre { get; }

    public ObservableCollection<LineaDespachoEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

    // --- Modo ---

    private TipoDespacho _tipo = TipoDespacho.Venta;
    public TipoDespacho Tipo
    {
        get => _tipo;
        set
        {
            // Notifica todas: enumerar aquí un OnPropertyChanged por cada Muestra… es la lista
            // que se queda corta el día que se agrega un modo más — mismo criterio que
            // EntradaEditorViewModel.Tipo.
            if (SetProperty(ref _tipo, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    /// <summary>Las dos pestañas, enlazadas en DOS VÍAS al <c>IsChecked</c> de su botón.</summary>
    public bool EsVenta
    {
        get => Tipo == TipoDespacho.Venta;
        set { if (value) Tipo = TipoDespacho.Venta; }
    }

    public bool EsAjuste
    {
        get => Tipo == TipoDespacho.Ajuste;
        set { if (value) Tipo = TipoDespacho.Ajuste; }
    }

    public bool MuestraCliente => Tipo == TipoDespacho.Venta;

    /// <summary>En un ajuste no se vende nada, así que no hay precios que pedir.</summary>
    public bool MuestraPrecios => Tipo == TipoDespacho.Venta;

    public string NotaModo => Tipo switch
    {
        TipoDespacho.Venta =>
            "Al registrarlo se creará la cuenta por cobrar en Finanzas · Cuentas por Cobrar.",
        _ => "Un ajuste solo corrige la existencia: no genera ninguna cuenta por cobrar."
    };

    // --- Cabecera ---

    private Cliente? _clienteSeleccionado;
    public Cliente? ClienteSeleccionado
    {
        get => _clienteSeleccionado;
        set => SetProperty(ref _clienteSeleccionado, value);
    }

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    public decimal TotalCantidad => Lineas.Sum(l => l.CantidadValor);

    public decimal Total => Lineas.Sum(l => l.Subtotal);

    public string TotalTexto => Total.ToString("N2");

    protected override bool Validar(out string? error)
    {
        if (Lineas.Any(l => l.ProductoSeleccionado is not null && !l.CantidadEsValida))
        {
            error = "Hay una cantidad que no es un número válido.";
            return false;
        }

        if (MuestraPrecios && Lineas.Any(l => l.ProductoSeleccionado is not null && !l.PrecioEsValido))
        {
            error = "Hay un precio que no es un número válido.";
            return false;
        }

        // La autoridad es el servicio, que vuelve a mirar la existencia real en el momento de
        // guardar: entre que se abrió el despacho y se emite, otro puesto pudo haber despachado.
        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override Despacho ObtenerResultado()
    {
        var despacho = _original.Clonar();
        despacho.Tipo = Tipo;
        despacho.Fecha = Fecha.Date;
        despacho.Observaciones = Observaciones.Trim();

        despacho.ClienteId = Tipo == TipoDespacho.Venta ? ClienteSeleccionado?.Id : null;
        despacho.ClienteNombre = Tipo == TipoDespacho.Venta ? ClienteSeleccionado?.Nombre ?? string.Empty : string.Empty;

        despacho.Lineas = [.. Lineas
            .Where(l => l.ProductoSeleccionado is not null)
            .Select(l => l.Construir(MuestraPrecios))];

        despacho.Total = despacho.Lineas.Sum(l => l.Subtotal);

        return despacho;
    }

    private LineaDespachoEditorViewModel NuevaLinea()
    {
        var linea = new LineaDespachoEditorViewModel(Productos, _existencias, _procesosTerminados);
        linea.Cambio += AlCambiarLinea;
        return linea;
    }

    private void AlCambiarLineas(object? remitente, NotifyCollectionChangedEventArgs e)
    {
        foreach (var linea in e.OldItems?.OfType<LineaDespachoEditorViewModel>() ?? [])
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
/// Un renglón del despacho mientras se escribe. Como el de las salidas de materia prima, con la
/// existencia disponible a la vista: avisa en el acto de que se está pidiendo de más, sin esperar
/// a que el servicio rechace el despacho entero. Gana precio/subtotal, mismo patrón "vacío no es
/// cero" que <see cref="LineaEntradaEditorViewModel"/>.
/// </summary>
public sealed class LineaDespachoEditorViewModel : ViewModelBase
{
    public event EventHandler? Cambio;

    private readonly IReadOnlyDictionary<int, decimal> _existencias;
    private readonly IReadOnlyList<ProcesoProduccion> _procesosTerminados;

    public LineaDespachoEditorViewModel(IReadOnlyList<Producto> productos,
                                        IReadOnlyDictionary<int, decimal> existencias,
                                        IReadOnlyList<ProcesoProduccion> procesosTerminados)
    {
        Productos = productos;
        _existencias = existencias;
        _procesosTerminados = procesosTerminados;
    }

    public IReadOnlyList<Producto> Productos { get; }

    private Producto? _productoSeleccionado;
    public Producto? ProductoSeleccionado
    {
        get => _productoSeleccionado;
        set
        {
            if (!SetProperty(ref _productoSeleccionado, value))
                return;

            // Propone el precio del catálogo solo si el operador todavía no escribió nada en esta
            // línea; si ya había un precio a mano, cambiar de producto no lo pisa.
            if (value is { PrecioUnitario: > 0 } && string.IsNullOrWhiteSpace(PrecioUnitario))
                PrecioUnitario = value.PrecioUnitario.ToString("0.####");

            Recalcular();
        }
    }

    /// <summary>Solo los procesos que fabricaron el producto elegido en esta línea — la lista se
    /// reduce en el acto al cambiar de producto, igual que <see cref="Disponible"/>.</summary>
    public IReadOnlyList<ProcesoProduccion> ProcesosDelProducto =>
        ProductoSeleccionado is null
            ? []
            : [.. _procesosTerminados.Where(p => p.ProductoId == ProductoSeleccionado.Id)];

    /// <summary>De qué proceso salió lo despachado, si el operador lo sabe. Opcional a propósito:
    /// no se valida ni se descuenta nada de él — ver el comentario de <see cref="DespachoLinea.ProcesoProduccionId"/>.</summary>
    private ProcesoProduccion? _procesoSeleccionado;
    public ProcesoProduccion? ProcesoSeleccionado
    {
        get => _procesoSeleccionado;
        set => SetProperty(ref _procesoSeleccionado, value);
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

    public decimal Disponible => ProductoSeleccionado is { } producto ? _existencias.GetValueOrDefault(producto.Id) : 0;

    public string DisponibleTexto => ProductoSeleccionado is null ? string.Empty : $"{Disponible:N2}";

    /// <summary>Se pinta en rojo en la grilla; la comprobación de verdad la hace el servicio.</summary>
    public bool SePasa => ProductoSeleccionado is not null && CantidadValor > Disponible;

    /// <summary>Cero en un Ajuste: ahí no se vendió nada — mismo criterio que
    /// <see cref="LineaEntradaEditorViewModel.Construir"/>.</summary>
    public DespachoLinea Construir(bool conPrecios) => new()
    {
        ProductoId = ProductoSeleccionado!.Id,
        ProductoNombre = ProductoSeleccionado.Nombre,
        UnidadMedidaSnapshot = ProductoSeleccionado.UnidadMedida,
        Cantidad = CantidadValor,
        PrecioUnitario = conPrecios ? PrecioValor : 0m,
        Subtotal = conPrecios ? Subtotal : 0m,
        ProcesoProduccionId = ProcesoSeleccionado?.Id,
        ProcesoProduccionNumero = ProcesoSeleccionado?.Numero ?? string.Empty
    };

    private void Recalcular()
    {
        OnPropertyChanged(nameof(Disponible));
        OnPropertyChanged(nameof(DisponibleTexto));
        OnPropertyChanged(nameof(SePasa));
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalTexto));
        OnPropertyChanged(nameof(ProcesosDelProducto));

        // Un proceso elegido para un producto ya no aplica si la línea cambió de producto.
        if (ProcesoSeleccionado is { } proceso && !ProcesosDelProducto.Contains(proceso))
            ProcesoSeleccionado = null;

        Cambio?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Procesos · Despacho: el catálogo de productos y el historial de despachos, en una pantalla
/// conmutable, mismo patrón que <see cref="ProcesosProduccionViewModel"/>.
/// </summary>
public sealed class DespachosViewModel : PantallaViewModelBase
{
    public const string VistaProductos = "Productos";
    public const string VistaDespachos = "Despachos";

    public DespachosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private DespachosViewModel(Modulo modulo,
                               Submodulo submodulo,
                               IServicioDialogo dialogos,
                               ISesionActual sesion)
        : base(modulo, submodulo)
    {
        var productosDs = DataSourceFactory.CrearProductos();
        var procesosDs = DataSourceFactory.CrearProcesosProduccion();
        var despachosDs = DataSourceFactory.CrearDespachos();
        var clientesDs = DataSourceFactory.CrearClientes();
        var facturasClienteDs = DataSourceFactory.CrearFacturasCliente();

        var productosServicio = new ProductosService(productosDs, procesosDs, despachosDs);

        // Misma cadena de dependencias que arma Cuentas por Cobrar: el despacho necesita al
        // servicio de Finanzas para dejar la cuenta por cobrar cuando es una Venta, y ese
        // servicio necesita al de Banco.
        var banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                     DataSourceFactory.CrearCuentasBancarias(), sesion);
        var cuentasPorCobrar = new CuentasPorCobrarService(facturasClienteDs, banco, sesion);

        var servicio = new DespachosService(despachosDs, productosServicio, facturasClienteDs,
                                            cuentasPorCobrar, sesion);

        Productos = new ProductosCrudViewModel(productosDs, productosServicio, dialogos, sesion);
        Despachos = new DespachosCrudViewModel(despachosDs, productosServicio, clientesDs, procesosDs, servicio, dialogos, sesion);

        CambiarVistaCommand = new RelayCommand<string>(vista => VistaActual = vista);
    }

    public ProductosCrudViewModel Productos { get; }
    public DespachosCrudViewModel Despachos { get; }

    /// <summary>Los dos listados, aunque solo se vea uno: un producto nuevo tiene que ofrecerse
    /// al despachar, y un despacho nuevo cambia la existencia que muestra la otra pestaña.</summary>
    public override void Recargar()
    {
        Productos.Recargar();
        Despachos.Recargar();
    }

    private string _vistaActual = VistaDespachos;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarProductos => VistaActual == VistaProductos;
    public bool MostrarDespachos => VistaActual == VistaDespachos;

    public bool EsProductos
    {
        get => MostrarProductos;
        set { if (value) VistaActual = VistaProductos; }
    }

    public bool EsDespachos
    {
        get => MostrarDespachos;
        set { if (value) VistaActual = VistaDespachos; }
    }

    public ICommand CambiarVistaCommand { get; }
}
