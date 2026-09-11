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
/// El catálogo de etapas reutilizables. Sub-listado de Procesos · Producción.
/// </summary>
public sealed class EtapasProduccionCrudViewModel : CrudViewModelBase<EtapaProduccion, int>
{
    private readonly EtapasProduccionService _servicio;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

    public EtapasProduccionCrudViewModel(IEtapaProduccionDataSource etapas,
                                         EtapasProduccionService servicio,
                                         IServicioDialogo dialogos,
                                         ISesionActual sesion)
        : base(etapas, dialogos, sesion)
    {
        _servicio = servicio;
        _dialogos = dialogos;
        _sesion = sesion;

        CargarSugeridosCommand = new RelayCommand(CargarSugeridos, () => _sesion.Puede($"{ModuloPermiso}.Crear"));
    }

    public ICommand CargarSugeridosCommand { get; }

    public string Resumen => $"{_servicio.TotalActivas()} etapas activas";

    protected override string ModuloPermiso => "EtapasProduccion";

    protected override bool CoincideBusqueda(EtapaProduccion item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase);

    /// <summary>Una etapa que ya se usó en algún proceso no se borra, se desactiva.</summary>
    protected override bool PuedeEliminar(EtapaProduccion item) => _servicio.PuedeEliminar(item);

    protected override EtapaProduccion CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<EtapaProduccion> CrearEditor(EtapaProduccion item) =>
        new EtapaProduccionEditorViewModel(item, _servicio);

    /// <summary>Precarga el catálogo sugerido para una planta láctea; no duplica lo que ya
    /// exista, así que se puede pulsar más de una vez sin riesgo.</summary>
    private void CargarSugeridos()
    {
        var creadas = _servicio.CargarSugeridas(CatalogoLacteoSugerido.EtapasProduccion);
        Recargar();

        _dialogos.Informar("Catálogo cargado",
            creadas > 0
                ? $"Se agregaron {creadas} etapas sugeridas."
                : "Las etapas sugeridas ya estaban todas cargadas.");
    }
}

/// <summary>Alta/edición de una etapa del catálogo.</summary>
public sealed class EtapaProduccionEditorViewModel : CrudEditorViewModelBase<EtapaProduccion>
{
    private readonly EtapaProduccion _original;
    private readonly EtapasProduccionService _servicio;

    public EtapaProduccionEditorViewModel(EtapaProduccion original, EtapasProduccionService servicio)
    {
        _original = original;
        _servicio = servicio;

        Nombre = original.Nombre;
        Descripcion = original.Descripcion;
        Orden = original.Orden;
        Activo = original.Id == 0 || original.Activo;
    }

    public override string Titulo => _original.Id == 0 ? "Nueva etapa de producción" : $"Editar {_original.Nombre}";

    public override double AnchoEditor => Ancho.Estandar;

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _descripcion = string.Empty;
    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    private int _orden;
    public int Orden
    {
        get => _orden;
        set => SetProperty(ref _orden, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error) => _servicio.Validar(ObtenerResultado(), out error);

    public override EtapaProduccion ObtenerResultado()
    {
        var etapa = _original.Clonar();
        etapa.Nombre = Nombre.Trim();
        etapa.Descripcion = Descripcion.Trim();
        etapa.Orden = Orden;
        etapa.Activo = Activo;
        return etapa;
    }
}

/// <summary>
/// Procesos · Producción: el historial de fabricación y las cuatro acciones propias del
/// documento (iniciar, agregar etapa, terminar, anular). No hay Editar ni Eliminar: un proceso
/// es un documento con máquina de estados.
/// </summary>
public sealed class ProcesosProduccionCrudViewModel : CrudViewModelBase<ProcesoProduccion, int>
{
    private const string FiltroTodos = "Todos";

    private readonly ProcesosProduccionService _servicio;
    private readonly ProductosService _productos;
    private readonly IEtapaProduccionDataSource _etapas;
    private readonly ITipoMateriaPrimaDataSource _tipos;
    private readonly IArticuloDataSource _articulos;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;

    private string _filtro = FiltroTodos;

    public ProcesosProduccionCrudViewModel(IProcesoProduccionDataSource procesos,
                                           ProcesosProduccionService servicio,
                                           ProductosService productos,
                                           IEtapaProduccionDataSource etapas,
                                           ITipoMateriaPrimaDataSource tipos,
                                           IArticuloDataSource articulos,
                                           IServicioDialogo dialogos,
                                           ISesionActual sesion)
        : base(procesos, dialogos, sesion)
    {
        _servicio = servicio;
        _productos = productos;
        _etapas = etapas;
        _tipos = tipos;
        _articulos = articulos;
        _dialogos = dialogos;
        _sesionActual = sesion;

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AgregarEtapaCommand = new RelayCommand(AgregarEtapa,
            () => SelectedItem is { } p && _servicio.PuedeAgregarEtapa(p)
                  && _sesionActual.Puede(Permisos.ProcesosProduccion.AgregarEtapa));

        TerminarCommand = new RelayCommand(Terminar,
            () => SelectedItem is { } p && _servicio.PuedeTerminar(p)
                  && _sesionActual.Puede(Permisos.ProcesosProduccion.Terminar));

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } p && _servicio.PuedeAnular(p)
                  && _sesionActual.Puede(Permisos.ProcesosProduccion.Anular));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AgregarEtapaCommand { get; }
    public ICommand TerminarCommand { get; }
    public ICommand AnularCommand { get; }

    public string Resumen =>
        $"{_servicio.EnProcesoCount()} en proceso · {_servicio.DelMes().Count} este mes";

    protected override string ModuloPermiso => "ProcesosProduccion";

    protected override bool CoincideBusqueda(ProcesoProduccion item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ProductoNombre.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(ProcesoProduccion item) => _filtro switch
    {
        "En proceso" => item.Estado == EstadoProcesoProduccion.EnProceso,
        "Terminados" => item.Estado == EstadoProcesoProduccion.Terminado,
        "Anulados" => item.Estado == EstadoProcesoProduccion.Anulado,
        _ => true
    };

    /// <summary>Un proceso no se corrige ni se borra: avanza por su máquina de estados.</summary>
    protected override bool PuedeEditar(ProcesoProduccion item) => false;

    protected override bool PuedeEliminar(ProcesoProduccion item) => false;

    protected override ProcesoProduccion CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Estado = EstadoProcesoProduccion.EnProceso
    };

    protected override CrudEditorViewModelBase<ProcesoProduccion> CrearEditor(ProcesoProduccion item) =>
        new IniciarProcesoEditorViewModel(item, _productos.ActivosConExistencia(), ListaTipos(), ListaArticulos(), _servicio);

    private IReadOnlyList<TipoMateriaPrima> ListaTipos() =>
        [.. _tipos.GetActivos().OrderBy(t => t.Nombre)];

    private IReadOnlyList<Articulo> ListaArticulos() =>
        [.. _articulos.GetActivos().OrderBy(a => a.Nombre)];

    private IReadOnlyList<EtapaProduccion> ListaEtapasActivas() =>
        [.. _etapas.GetActivas().OrderBy(e => e.Orden).ThenBy(e => e.Nombre)];

    /// <summary>
    /// Iniciar pasa por el servicio de dominio, igual que las Recepciones/Salidas de Materia
    /// Prima: es él quien asigna el correlativo, pre-valida el consumo y registra las Salidas
    /// enlazadas.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Iniciar(editor.ObtenerResultado(), _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void AgregarEtapa()
    {
        if (SelectedItem is not { } proceso)
            return;

        var editor = new EtapaProcesoEditorViewModel(proceso, ListaEtapasActivas(), ListaTipos(), ListaArticulos());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.AgregarEtapa(proceso, editor.ObtenerResultado(), _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Terminar()
    {
        if (SelectedItem is not { } proceso)
            return;

        var editor = new TerminarProcesoEditorViewModel(proceso);

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Terminar(proceso, editor.CantidadProducidaValor, editor.Observaciones,
                                         _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } proceso)
            return;

        // El texto deja explícito que una merma o un defecto de calidad detectado DESPUÉS de
        // terminar es un motivo esperado, no una excepción rara — así lo pidió el negocio.
        var editor = new MotivoEditorViewModel(
            $"Anular proceso {proceso.Numero}",
            $"{proceso.ProductoNombre} — {proceso.EstadoTexto}. Anular un proceso Terminado por una " +
            "merma o un defecto de calidad detectado después es un caso normal y esperado, no una " +
            "excepción; descríbalo en el motivo.",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(proceso, editor.Motivo));
    }

    /// <summary>
    /// La lista la repuebla la recarga que dispara la escritura del servicio; aquí solo se apunta
    /// qué proceso dejar seleccionado y se traduce el rechazo de una regla en un aviso.
    /// </summary>
    private void Aplicar(Func<ProcesoProduccion> transicion)
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

/// <summary>El modal de "Iniciar proceso": el producto a fabricar y el consumo inicial.</summary>
public sealed class IniciarProcesoEditorViewModel : CrudEditorViewModelBase<ProcesoProduccion>
{
    private readonly ProcesoProduccion _original;
    private readonly ProcesosProduccionService _servicio;

    public IniciarProcesoEditorViewModel(ProcesoProduccion original,
                                         IReadOnlyList<Producto> productos,
                                         IReadOnlyList<TipoMateriaPrima> tiposMateriaPrima,
                                         IReadOnlyList<Articulo> articulos,
                                         ProcesosProduccionService servicio)
    {
        _original = original;
        _servicio = servicio;

        Productos = productos;
        TiposMateriaPrima = tiposMateriaPrima;
        Articulos = articulos;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        ProductoSeleccionado = productos.FirstOrDefault(p => p.Id == original.ProductoId);
        CantidadPlaneada = original.CantidadPlaneada > 0 ? original.CantidadPlaneada.ToString("0.####") : string.Empty;
        Observaciones = original.Observaciones;

        AgregarLineaCommand = new RelayCommand(() => LineasIniciales.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaConsumoEditorViewModel>(linea =>
        {
            if (linea is not null)
                LineasIniciales.Remove(linea);
        });

        LineasIniciales.Add(NuevaLinea());
    }

    public override string Titulo => "Iniciar proceso de producción";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Iniciar proceso";

    public IReadOnlyList<Producto> Productos { get; }
    public IReadOnlyList<TipoMateriaPrima> TiposMateriaPrima { get; }
    public IReadOnlyList<Articulo> Articulos { get; }

    public ObservableCollection<LineaConsumoEditorViewModel> LineasIniciales { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private Producto? _productoSeleccionado;
    public Producto? ProductoSeleccionado
    {
        get => _productoSeleccionado;
        set => SetProperty(ref _productoSeleccionado, value);
    }

    private string _cantidadPlaneada = string.Empty;
    public string CantidadPlaneada
    {
        get => _cantidadPlaneada;
        set => SetProperty(ref _cantidadPlaneada, value);
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    public bool CantidadPlaneadaEsValida =>
        !string.IsNullOrWhiteSpace(CantidadPlaneada) && decimal.TryParse(CantidadPlaneada, out var v) && v > 0;

    public decimal CantidadPlaneadaValor => decimal.TryParse(CantidadPlaneada, out var valor) ? valor : 0m;

    protected override bool Validar(out string? error)
    {
        if (ProductoSeleccionado is null)
        {
            error = "Seleccione el producto que va a fabricar.";
            return false;
        }

        if (!CantidadPlaneadaEsValida)
        {
            error = "La cantidad planeada debe ser un número válido mayor que cero.";
            return false;
        }

        if (LineasIniciales.Any(l => l.TieneMaterialSeleccionado && !l.CantidadEsValida))
        {
            error = "Hay una cantidad de consumo que no es un número válido.";
            return false;
        }

        // La autoridad final es el servicio: aquí solo se le pregunta, para que el mensaje salga
        // en el formulario en vez de en un diálogo de error después de cerrarlo.
        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override ProcesoProduccion ObtenerResultado()
    {
        var proceso = _original.Clonar();
        proceso.Fecha = Fecha.Date;
        proceso.ProductoId = ProductoSeleccionado?.Id ?? 0;
        proceso.ProductoNombre = ProductoSeleccionado?.Nombre ?? string.Empty;
        proceso.UnidadMedidaSnapshot = ProductoSeleccionado?.UnidadMedida ?? string.Empty;
        proceso.CantidadPlaneada = CantidadPlaneadaValor;
        proceso.Observaciones = Observaciones.Trim();

        // Las líneas en blanco no se mandan: consumir nada al iniciar es válido (se puede
        // consumir todo por etapas), y una fila vacía al final es lo normal mientras se carga.
        proceso.LineasIniciales = [.. LineasIniciales
            .Where(l => l.TieneMaterialSeleccionado)
            .Select(l => l.ConstruirLineaInicial()!)];

        return proceso;
    }

    private LineaConsumoEditorViewModel NuevaLinea() => new(TiposMateriaPrima, Articulos);
}

/// <summary>El modal de "Agregar etapa": qué etapa del catálogo y el consumo opcional que hizo falta.</summary>
public sealed class EtapaProcesoEditorViewModel : CrudEditorViewModelBase<EtapaProcesoProduccion>
{
    private readonly string _titulo;

    public EtapaProcesoEditorViewModel(ProcesoProduccion proceso,
                                       IReadOnlyList<EtapaProduccion> etapas,
                                       IReadOnlyList<TipoMateriaPrima> tiposMateriaPrima,
                                       IReadOnlyList<Articulo> articulos)
    {
        _titulo = $"Agregar etapa al proceso {proceso.Numero}";

        Etapas = etapas;
        TiposMateriaPrima = tiposMateriaPrima;
        Articulos = articulos;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaConsumoEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    public override string Titulo => _titulo;

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Agregar etapa";

    public IReadOnlyList<EtapaProduccion> Etapas { get; }
    public IReadOnlyList<TipoMateriaPrima> TiposMateriaPrima { get; }
    public IReadOnlyList<Articulo> Articulos { get; }

    public ObservableCollection<LineaConsumoEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

    private EtapaProduccion? _etapaSeleccionada;
    public EtapaProduccion? EtapaSeleccionada
    {
        get => _etapaSeleccionada;
        set => SetProperty(ref _etapaSeleccionada, value);
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    protected override bool Validar(out string? error)
    {
        if (EtapaSeleccionada is null)
        {
            error = "Seleccione la etapa.";
            return false;
        }

        if (Lineas.Any(l => l.TieneMaterialSeleccionado && !l.CantidadEsValida))
        {
            error = "Hay una cantidad de consumo que no es un número válido.";
            return false;
        }

        error = null;
        return true;
    }

    public override EtapaProcesoProduccion ObtenerResultado() => new()
    {
        EtapaProduccionId = EtapaSeleccionada?.Id ?? 0,
        EtapaProduccionNombre = EtapaSeleccionada?.Nombre ?? string.Empty,
        Observaciones = Observaciones.Trim(),

        // El consumo de una etapa es opcional: las líneas en blanco no se mandan, y quedarse sin
        // ninguna es válido (etapas como "Reposo" o "Etiquetado" no siempre consumen nada nuevo).
        Lineas = [.. Lineas.Where(l => l.TieneMaterialSeleccionado).Select(l => l.ConstruirLineaDeEtapa()!)]
    };

    private LineaConsumoEditorViewModel NuevaLinea() => new(TiposMateriaPrima, Articulos);
}

/// <summary>
/// El modal de "Terminar proceso": no edita una entidad con <c>Id</c> propio, así que hereda de
/// la base no genérica —mismo criterio que <see cref="MotivoEditorViewModel"/>—, y el ViewModel
/// padre lee <see cref="CantidadProducidaValor"/>/<see cref="Observaciones"/> para llamar al
/// servicio.
/// </summary>
public sealed class TerminarProcesoEditorViewModel : CrudEditorViewModelBase
{
    private readonly ProcesoProduccion _proceso;

    public TerminarProcesoEditorViewModel(ProcesoProduccion proceso)
    {
        _proceso = proceso;
        CantidadProducida = proceso.CantidadPlaneada > 0 ? proceso.CantidadPlaneada.ToString("0.####") : string.Empty;
    }

    public override string Titulo => $"Terminar proceso {_proceso.Numero}";

    public override double AnchoEditor => Ancho.Compacto;

    public override string TextoAccion => "Terminar proceso";

    public string ProductoTexto => $"{_proceso.ProductoNombre} — planeado {_proceso.CantidadPlaneadaTexto}";

    private string _cantidadProducida = string.Empty;
    public string CantidadProducida
    {
        get => _cantidadProducida;
        set => SetProperty(ref _cantidadProducida, value);
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    public bool CantidadProducidaEsValida =>
        !string.IsNullOrWhiteSpace(CantidadProducida) && decimal.TryParse(CantidadProducida, out var v) && v > 0;

    public decimal CantidadProducidaValor => decimal.TryParse(CantidadProducida, out var valor) ? valor : 0m;

    protected override bool Validar(out string? error)
    {
        if (!CantidadProducidaEsValida)
        {
            error = "Indique cuánto se produjo, con un número mayor que cero. Puede diferir de lo " +
                    "planeado: una merma es justo el dato que interesa registrar.";
            return false;
        }

        error = null;
        return true;
    }
}

/// <summary>
/// Un renglón de consumo, inicial o de etapa: elige el origen del material —materia prima o
/// artículo de inventario, indistintamente— y de ahí el material concreto.
///
/// Es un ViewModel y no <see cref="ProcesoProduccionLineaInicial"/>/<see cref="EtapaProcesoProduccionLinea"/>
/// directamente porque hace falta un origen conmutable con dos listas de selección detrás, y los
/// modelos no avisan de sus cambios. La cantidad viaja como texto por el mismo motivo que en el
/// resto de los editores: un campo vacío no es un cero.
/// </summary>
public sealed class LineaConsumoEditorViewModel : ViewModelBase
{
    /// <summary>Avisa a quien la contenga de que algo cambió, mismo patrón que
    /// <see cref="LineaEntradaEditorViewModel"/>. Este editor no suma un total (las unidades se
    /// mezclan entre líneas), pero el evento se deja para que un futuro resumen no tenga que
    /// reinventar el enganche.</summary>
    public event EventHandler? Cambio;

    public LineaConsumoEditorViewModel(IReadOnlyList<TipoMateriaPrima> tiposMateriaPrima, IReadOnlyList<Articulo> articulos)
    {
        TiposMateriaPrima = tiposMateriaPrima;
        Articulos = articulos;
    }

    public IReadOnlyList<TipoMateriaPrima> TiposMateriaPrima { get; }
    public IReadOnlyList<Articulo> Articulos { get; }

    private OrigenMaterial _origenSeleccionado = OrigenMaterial.MateriaPrima;
    public OrigenMaterial OrigenSeleccionado
    {
        get => _origenSeleccionado;
        set
        {
            if (!SetProperty(ref _origenSeleccionado, value))
                return;

            // Cambiar de origen deja seleccionado —pero invisible— un material del origen
            // contrario; sin limpiarlo, Construir() podría armar una línea con el Id de un tipo
            // de materia prima y el nombre/unidad de un artículo, o viceversa.
            if (EsMateriaPrima)
                ArticuloSeleccionado = null;
            else
                TipoSeleccionado = null;

            OnPropertyChanged(nameof(EsMateriaPrima));
            OnPropertyChanged(nameof(EsArticulo));
            Recalcular();
        }
    }

    public bool EsMateriaPrima => OrigenSeleccionado == OrigenMaterial.MateriaPrima;
    public bool EsArticulo => !EsMateriaPrima;

    private TipoMateriaPrima? _tipoSeleccionado;
    public TipoMateriaPrima? TipoSeleccionado
    {
        get => _tipoSeleccionado;
        set { if (SetProperty(ref _tipoSeleccionado, value)) Recalcular(); }
    }

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

    /// <summary>Una línea está en blanco si no se eligió ni tipo ni artículo; la usa el editor
    /// padre para filtrarla al construir el resultado.</summary>
    public bool TieneMaterialSeleccionado => EsMateriaPrima ? TipoSeleccionado is not null : ArticuloSeleccionado is not null;

    public bool CantidadEsValida =>
        !string.IsNullOrWhiteSpace(Cantidad) && decimal.TryParse(Cantidad, out var v) && v > 0;

    public decimal CantidadValor => decimal.TryParse(Cantidad, out var valor) ? valor : 0m;

    public string UnidadTexto => EsMateriaPrima
        ? TipoSeleccionado?.UnidadMedida ?? string.Empty
        : ArticuloSeleccionado?.UnidadCorta ?? string.Empty;

    private int MaterialId => EsMateriaPrima ? TipoSeleccionado?.Id ?? 0 : ArticuloSeleccionado?.Id ?? 0;

    /// <summary>Usa <see cref="Articulo.Etiqueta"/> (código · nombre) cuando el origen es
    /// Inventario, para que el snapshot quede trazable hasta el artículo del almacén.</summary>
    private string MaterialNombre => EsMateriaPrima
        ? TipoSeleccionado?.Nombre ?? string.Empty
        : ArticuloSeleccionado?.Etiqueta ?? string.Empty;

    public ProcesoProduccionLineaInicial? ConstruirLineaInicial()
    {
        if (!TieneMaterialSeleccionado)
            return null;

        return new ProcesoProduccionLineaInicial
        {
            Origen = OrigenSeleccionado,
            MaterialId = MaterialId,
            MaterialNombre = MaterialNombre,
            UnidadMedidaSnapshot = UnidadTexto,
            Cantidad = CantidadValor
        };
    }

    public EtapaProcesoProduccionLinea? ConstruirLineaDeEtapa()
    {
        if (!TieneMaterialSeleccionado)
            return null;

        return new EtapaProcesoProduccionLinea
        {
            Origen = OrigenSeleccionado,
            MaterialId = MaterialId,
            MaterialNombre = MaterialNombre,
            UnidadMedidaSnapshot = UnidadTexto,
            Cantidad = CantidadValor
        };
    }

    private void Recalcular()
    {
        OnPropertyChanged(nameof(UnidadTexto));
        OnPropertyChanged(nameof(TieneMaterialSeleccionado));
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Procesos · Producción: el catálogo de etapas y el historial de fabricación, en una pantalla
/// conmutable, mismo patrón que <see cref="MovimientosViewModel"/>.
/// </summary>
public sealed class ProcesosProduccionViewModel : PantallaViewModelBase
{
    public const string VistaEtapas = "Etapas";
    public const string VistaProcesos = "Procesos";

    public ProcesosProduccionViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ProcesosProduccionViewModel(Modulo modulo,
                                        Submodulo submodulo,
                                        IServicioDialogo dialogos,
                                        ISesionActual sesion)
        : base(modulo, submodulo)
    {
        var etapasDs = DataSourceFactory.CrearEtapasProduccion();
        var procesosDs = DataSourceFactory.CrearProcesosProduccion();
        var productosDs = DataSourceFactory.CrearProductos();
        var despachosDs = DataSourceFactory.CrearDespachos();
        var tiposDs = DataSourceFactory.CrearTiposMateriaPrima();
        var articulosDs = DataSourceFactory.CrearArticulos();

        var etapasServicio = new EtapasProduccionService(etapasDs, procesosDs);
        var productosServicio = new ProductosService(productosDs, procesosDs, despachosDs);

        var materiaPrima = new MateriaPrimaService(tiposDs, DataSourceFactory.CrearRecepcionesMateriaPrima(),
                                                   DataSourceFactory.CrearSalidasMateriaPrima());
        var inventario = new InventarioService(articulosDs, DataSourceFactory.CrearEntradasInventario(),
                                               DataSourceFactory.CrearSalidasInventario());

        var salidasMateriaPrimaServicio = new SalidasMateriaPrimaService(
            DataSourceFactory.CrearSalidasMateriaPrima(), materiaPrima, sesion);
        var salidasInventarioServicio = new SalidasInventarioService(
            DataSourceFactory.CrearSalidasInventario(), inventario, sesion);

        var procesosServicio = new ProcesosProduccionService(procesosDs, productosServicio,
            salidasMateriaPrimaServicio, salidasInventarioServicio, articulosDs, sesion);

        Etapas = new EtapasProduccionCrudViewModel(etapasDs, etapasServicio, dialogos, sesion);

        Procesos = new ProcesosProduccionCrudViewModel(procesosDs, procesosServicio, productosServicio,
            etapasDs, tiposDs, articulosDs, dialogos, sesion);

        CambiarVistaCommand = new RelayCommand<string>(vista => VistaActual = vista);
    }

    public EtapasProduccionCrudViewModel Etapas { get; }
    public ProcesosProduccionCrudViewModel Procesos { get; }

    /// <summary>Los dos listados, aunque solo se vea uno: una etapa nueva tiene que ofrecerse al
    /// agregar etapas, y un proceso nuevo cambia la existencia que muestra Despacho.</summary>
    public override void Recargar()
    {
        Etapas.Recargar();
        Procesos.Recargar();
    }

    private string _vistaActual = VistaProcesos;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarEtapas => VistaActual == VistaEtapas;
    public bool MostrarProcesos => VistaActual == VistaProcesos;

    public bool EsEtapas
    {
        get => MostrarEtapas;
        set { if (value) VistaActual = VistaEtapas; }
    }

    public bool EsProcesos
    {
        get => MostrarProcesos;
        set { if (value) VistaActual = VistaProcesos; }
    }

    public ICommand CambiarVistaCommand { get; }
}
