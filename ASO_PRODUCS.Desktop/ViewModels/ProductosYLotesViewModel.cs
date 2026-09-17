using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Procesos · Productos y Lotes: el catálogo de productos terminados con su existencia al día.
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
    private readonly ITipoMateriaPrimaDataSource _tipos;
    private readonly IArticuloDataSource _articulos;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    private string _filtro = FiltroTodos;

    public ProductosCrudViewModel(IProductoDataSource productos,
                                  ProductosService servicio,
                                  ITipoMateriaPrimaDataSource tipos,
                                  IArticuloDataSource articulos,
                                  IServicioDialogo dialogos,
                                  ISesionActual sesion)
        : base(productos, dialogos, sesion)
    {
        _servicio = servicio;
        _tipos = tipos;
        _articulos = articulos;
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
        $"{_servicio.TotalProductosActivos()} productos activos · {_servicio.ProductosSinExistencia()} sin existencia · " +
        $"{_servicio.ProductosBajoMinimo()} bajo mínimo";

    protected override string ModuloPermiso => "Productos";

    protected override bool CoincideBusqueda(Producto item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ProductoBaseNombre.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(Producto item) => _filtro switch
    {
        "Bajo mínimo" => item.BajoMinimo,
        "Sin existencia" => item.SinExistencia,
        "Inactivos" => !item.Activo,
        _ => true
    };

    /// <summary>Un producto que ya se produjo o se despachó no se borra, se desactiva.</summary>
    protected override bool PuedeEliminar(Producto item) => _servicio.PuedeEliminar(item);

    protected override Producto CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Producto> CrearEditor(Producto item) =>
        new ProductoEditorViewModel(item, _servicio, [.. Items],
                                    [.. _tipos.GetAll().OrderBy(t => t.Nombre)],
                                    [.. _articulos.GetAll().OrderBy(a => a.Nombre)]);

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

/// <summary>Alta/edición de un producto terminado, con su receta opcional de presentación.</summary>
public sealed class ProductoEditorViewModel : CrudEditorViewModelBase<Producto>
{
    private readonly Producto _original;
    private readonly ProductosService _servicio;

    public ProductoEditorViewModel(Producto original, ProductosService servicio,
                                   IReadOnlyList<Producto> productos,
                                   IReadOnlyList<TipoMateriaPrima> tiposMateriaPrima,
                                   IReadOnlyList<Articulo> articulos)
    {
        _original = original;
        _servicio = servicio;

        ProductosBase = [.. productos.Where(p => p.Id != original.Id && (p.Activo || p.Id == original.ProductoBaseId))
                                    .OrderBy(p => p.Nombre)];
        TiposMateriaPrima = tiposMateriaPrima;
        Articulos = articulos;

        Nombre = original.Nombre;
        UnidadMedida = original.UnidadMedida;
        Precio = original.PrecioUnitario > 0 ? original.PrecioUnitario.ToString("0.####") : string.Empty;
        DiasVidaUtil = original.DiasVidaUtil?.ToString() ?? string.Empty;
        Minimo = original.Minimo == 0 ? string.Empty : original.Minimo.ToString("0.##");
        Activo = original.Id == 0 || original.Activo;

        _esDerivado = original.EsDerivado;
        _productoBase = ProductosBase.FirstOrDefault(p => p.Id == original.ProductoBaseId);
        _cantidadBase = original.CantidadBasePorUnidad?.ToString("0.####") ?? string.Empty;

        foreach (var componente in original.Componentes)
            Componentes.Add(DesdeComponente(componente));

        AgregarComponenteCommand = new RelayCommand(() => Componentes.Add(NuevoComponente()));
        QuitarComponenteCommand = new RelayCommand<LineaConsumoEditorViewModel>(linea =>
        {
            if (linea is not null)
                Componentes.Remove(linea);
        });
    }

    public override string Titulo => _original.Id == 0 ? "Nuevo producto" : $"Editar {_original.Nombre}";

    /// <summary>Amplio: la receta de presentación lleva una grilla dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public IReadOnlyList<Producto> ProductosBase { get; }
    public IReadOnlyList<TipoMateriaPrima> TiposMateriaPrima { get; }
    public IReadOnlyList<Articulo> Articulos { get; }

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set { if (SetProperty(ref _nombre, value)) OnPropertyChanged(nameof(AyudaCantidadBase)); }
    }

    private string _unidadMedida = string.Empty;
    public string UnidadMedida
    {
        get => _unidadMedida;
        set { if (SetProperty(ref _unidadMedida, value)) OnPropertyChanged(nameof(AyudaCantidadBase)); }
    }

    // --- Presentación / subproducto ---

    private bool _esDerivado;
    public bool EsDerivado
    {
        get => _esDerivado;
        set => SetProperty(ref _esDerivado, value);
    }

    private Producto? _productoBase;
    public Producto? ProductoBase
    {
        get => _productoBase;
        set
        {
            if (!SetProperty(ref _productoBase, value))
                return;

            OnPropertyChanged(nameof(UnidadBase));
            OnPropertyChanged(nameof(AyudaCantidadBase));
        }
    }

    public string UnidadBase => ProductoBase?.UnidadMedida ?? string.Empty;

    private string _cantidadBase = string.Empty;
    public string CantidadBase
    {
        get => _cantidadBase;
        set { if (SetProperty(ref _cantidadBase, value)) OnPropertyChanged(nameof(AyudaCantidadBase)); }
    }

    /// <summary>La frase que confirma que el número se entendió bien: "Cada 1 unidad de Mantequilla
    /// 200 g consume 0,2 kg de Mantequilla".</summary>
    public string AyudaCantidadBase =>
        ProductoBase is { } producto && decimal.TryParse(CantidadBase, out var cantidad) && cantidad > 0
            ? $"Cada 1 {TextoO(UnidadMedida, "unidad")} de {TextoO(Nombre, "este producto")} consume " +
              $"{cantidad:0.####} {producto.UnidadMedida} de {producto.Nombre}."
            : "Cuánto del producto base (en su unidad) lleva cada unidad de este producto.";

    private static string TextoO(string texto, string siVacio) => string.IsNullOrWhiteSpace(texto) ? siVacio : texto.Trim();

    public ObservableCollection<LineaConsumoEditorViewModel> Componentes { get; } = [];

    public ICommand AgregarComponenteCommand { get; }
    public ICommand QuitarComponenteCommand { get; }

    private LineaConsumoEditorViewModel NuevoComponente() =>
        new(TiposMateriaPrima, Articulos, []) { OrigenSeleccionado = OrigenMaterial.Articulo };

    private LineaConsumoEditorViewModel DesdeComponente(ProductoComponente componente)
    {
        var linea = NuevoComponente();
        linea.OrigenSeleccionado = componente.Origen;

        if (componente.Origen == OrigenMaterial.MateriaPrima)
            linea.TipoSeleccionado = TiposMateriaPrima.FirstOrDefault(t => t.Id == componente.MaterialId);
        else
            linea.ArticuloSeleccionado = Articulos.FirstOrDefault(a => a.Id == componente.MaterialId);

        linea.Cantidad = componente.CantidadPorUnidad.ToString("0.####");
        return linea;
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

    /// <summary>Vacío = el producto no vence. Un texto que no es número entero se rechaza en
    /// <see cref="Validar"/>, para no tomarlo en silencio como "no vence".</summary>
    private string _diasVidaUtil = string.Empty;
    public string DiasVidaUtil
    {
        get => _diasVidaUtil;
        set => SetProperty(ref _diasVidaUtil, value);
    }

    private string _minimo = string.Empty;
    public string Minimo
    {
        get => _minimo;
        set => SetProperty(ref _minimo, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error)
    {
        if (!string.IsNullOrWhiteSpace(DiasVidaUtil) && !int.TryParse(DiasVidaUtil, out _))
        {
            error = "Los días de vida útil deben ser un número entero.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Minimo)
            && (!decimal.TryParse(Minimo, out var minimo) || minimo < 0))
        {
            error = "El mínimo debe ser un número mayor o igual a cero.";
            return false;
        }

        if (EsDerivado)
        {
            if (ProductoBase is null)
            {
                error = "Seleccione de qué producto se obtiene.";
                return false;
            }

            if (!decimal.TryParse(CantidadBase, out var cantidadBase) || cantidadBase <= 0)
            {
                error = $"Indique cuánto de {ProductoBase.Nombre} lleva cada unidad, con un número mayor que cero.";
                return false;
            }

            if (Componentes.Any(c => c.TieneMaterialSeleccionado && !c.CantidadEsValida))
            {
                error = "Hay una cantidad de empaque que no es un número válido.";
                return false;
            }
        }

        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override Producto ObtenerResultado()
    {
        var producto = _original.Clonar();
        producto.Nombre = Nombre.Trim();
        producto.UnidadMedida = UnidadMedida.Trim();
        producto.PrecioUnitario = decimal.TryParse(Precio, out var precio) ? precio : 0m;
        producto.DiasVidaUtil = int.TryParse(DiasVidaUtil, out var dias) ? dias : null;
        producto.Minimo = decimal.TryParse(Minimo, out var minimo) ? minimo : 0m;
        producto.Activo = Activo;

        // Desmarcar "se obtiene de otro producto" borra la receta entera, no la deja escondida.
        if (EsDerivado && ProductoBase is { } productoBase)
        {
            producto.ProductoBaseId = productoBase.Id;
            producto.ProductoBaseNombre = productoBase.Nombre;
            producto.CantidadBasePorUnidad = decimal.TryParse(CantidadBase, out var cantidad) ? cantidad : null;
            producto.Componentes = [.. Componentes.Where(c => c.TieneMaterialSeleccionado).Select(c => c.ConstruirComponente()!)];
        }
        else
        {
            producto.ProductoBaseId = null;
            producto.ProductoBaseNombre = string.Empty;
            producto.CantidadBasePorUnidad = null;
            producto.Componentes = [];
        }

        return producto;
    }
}

/// <summary>
/// Procesos · Productos y Lotes, pestaña Lotes: la existencia por lote con su vencimiento, de solo
/// lectura.
/// </summary>
public sealed class LotesViewModel : ViewModelBase
{
    private const string FiltroTodos = "Todos";

    private readonly ProductosService _productos;
    private readonly TransformacionesService _transformaciones;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;
    private readonly Action _alTransformar;
    private IReadOnlyList<LoteProducto> _todos = [];
    private string _filtro = FiltroTodos;

    public LotesViewModel(ProductosService productos, TransformacionesService transformaciones,
                          IServicioDialogo dialogos, ISesionActual sesion, Action alTransformar)
    {
        _productos = productos;
        _transformaciones = transformaciones;
        _dialogos = dialogos;
        _sesion = sesion;
        _alTransformar = alTransformar;

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            Filtrar();
        });

        TransformarCommand = new RelayCommand(Transformar,
            () => SelectedItem is not null
                  && _sesion.Puede(Permisos.ProcesosProduccion.Crear)
                  && _sesion.Puede(Permisos.ProcesosProduccion.Terminar));

        Recargar();
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand TransformarCommand { get; }

    public ObservableCollection<LoteProducto> Items { get; } = [];

    private LoteProducto? _selectedItem;
    public LoteProducto? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    private void Transformar()
    {
        if (SelectedItem is not { } lote)
            return;

        if (TransformarLoteEditorViewModel.Abrir(_transformaciones, _dialogos, _sesion, lote) is { } proceso)
        {
            _alTransformar();
            _dialogos.Informar("Transformación registrada",
                proceso.Estado == EstadoProcesoProduccion.Terminado
                    ? $"Se creó el lote {proceso.Numero} con {proceso.CantidadProducidaTexto} de {proceso.ProductoNombre}."
                    : $"El proceso {proceso.Numero} de {proceso.ProductoNombre} quedó En proceso; continúelo desde Producción.");
        }
    }

    public string Resumen =>
        $"{_todos.Count} lotes con existencia · " +
        $"{_todos.Count(l => l.Estado == EstadoVencimientoLote.PorVencer)} por vencer · " +
        $"{_todos.Count(l => l.Estado == EstadoVencimientoLote.Vencido)} vencidos";

    public void Recargar()
    {
        _todos = _productos.LotesConExistencia();
        Filtrar();
        OnPropertyChanged(nameof(Resumen));
    }

    private void Filtrar()
    {
        Items.Clear();

        foreach (var lote in _todos.Where(l => _filtro switch
                 {
                     "Por vencer" => l.Estado == EstadoVencimientoLote.PorVencer,
                     "Vencidos" => l.Estado == EstadoVencimientoLote.Vencido,
                     _ => true
                 }))
            Items.Add(lote);
    }
}

/// <summary>
/// Procesos · Productos y Lotes: el catálogo de productos y la existencia por lote, en una
/// pantalla conmutable, mismo patrón que <see cref="ProcesosProduccionViewModel"/>.
/// </summary>
public sealed class ProductosYLotesViewModel : PantallaViewModelBase
{
    public const string VistaProductos = "Productos";
    public const string VistaLotes = "Lotes";

    public ProductosYLotesViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ProductosYLotesViewModel(Modulo modulo,
                               Submodulo submodulo,
                               IServicioDialogo dialogos,
                               ISesionActual sesion)
        : base(modulo, submodulo)
    {
        var productosDs = DataSourceFactory.CrearProductos();
        var procesosDs = DataSourceFactory.CrearProcesosProduccion();
        var despachosDs = DataSourceFactory.CrearDespachos();
        var tiposDs = DataSourceFactory.CrearTiposMateriaPrima();
        var articulosDs = DataSourceFactory.CrearArticulos();

        var productosServicio = new ProductosService(productosDs, procesosDs, despachosDs);

        var materiaPrima = new MateriaPrimaService(tiposDs, DataSourceFactory.CrearRecepcionesMateriaPrima(),
                                                   DataSourceFactory.CrearSalidasMateriaPrima());
        var inventario = new InventarioService(articulosDs, DataSourceFactory.CrearEntradasInventario(),
                                               DataSourceFactory.CrearSalidasInventario());

        var procesosServicio = new ProcesosProduccionService(procesosDs, productosServicio,
            new SalidasMateriaPrimaService(DataSourceFactory.CrearSalidasMateriaPrima(), materiaPrima, sesion),
            new SalidasInventarioService(DataSourceFactory.CrearSalidasInventario(), inventario, sesion),
            articulosDs, sesion);

        var transformaciones = new TransformacionesService(procesosServicio, productosServicio, materiaPrima, inventario);

        Productos = new ProductosCrudViewModel(productosDs, productosServicio, tiposDs, articulosDs, dialogos, sesion);
        Lotes = new LotesViewModel(productosServicio, transformaciones, dialogos, sesion, Recargar);

        CambiarVistaCommand = new RelayCommand<string>(vista => VistaActual = vista);
    }

    public ProductosCrudViewModel Productos { get; }
    public LotesViewModel Lotes { get; }

    /// <summary>Las dos pestañas, aunque solo se vea una: un producto nuevo tiene que ofrecerse
    /// al despachar, y un despacho hecho desde Pedidos cambia la existencia por lote.</summary>
    public override void Recargar()
    {
        Productos.Recargar();
        Lotes.Recargar();
    }

    private string _vistaActual = VistaProductos;
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
    public bool MostrarLotes => VistaActual == VistaLotes;

    public bool EsLotes
    {
        get => MostrarLotes;
        set { if (value) VistaActual = VistaLotes; }
    }

    public bool EsProductos
    {
        get => MostrarProductos;
        set { if (value) VistaActual = VistaProductos; }
    }

    public ICommand CambiarVistaCommand { get; }
}
