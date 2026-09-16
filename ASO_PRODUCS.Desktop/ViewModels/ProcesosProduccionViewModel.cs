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
        etapa.Activo = Activo;
        return etapa;
    }
}

/// <summary>
/// Procesos · Producción: el historial de fabricación y las tres acciones propias del documento
/// (iniciar, continuar —agregar etapa o terminar, según lo que se elija dentro del modal— y
/// anular). No hay Editar ni Eliminar: un proceso es un documento con máquina de estados.
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

        // Un solo botón para las dos transiciones: PuedeAgregarEtapa/PuedeTerminar son la misma
        // guarda (Estado == EnProceso), y se exigen los dos permisos porque el modal decide adentro
        // cuál de las dos pasa a llamar — no hay forma de saber cuál será antes de abrirlo.
        ContinuarProcesoCommand = new RelayCommand(ContinuarProceso,
            () => SelectedItem is { } p && _servicio.PuedeAgregarEtapa(p)
                  && _sesionActual.Puede(Permisos.ProcesosProduccion.AgregarEtapa)
                  && _sesionActual.Puede(Permisos.ProcesosProduccion.Terminar));

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } p && _servicio.PuedeAnular(p)
                  && _sesionActual.Puede(Permisos.ProcesosProduccion.Anular));

        VerDetalleCommand = new RelayCommand(VerDetalle);
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand ContinuarProcesoCommand { get; }
    public ICommand AnularCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla (ver <c>ProcesosProduccionView.xaml.cs</c>),
    /// nunca un botón — por eso no lleva <c>CanExecute</c>: la guarda de "hay algo seleccionado"
    /// vive dentro de <see cref="VerDetalle"/>. Es una consulta de solo lectura, así que funciona
    /// igual sin importar el <see cref="EstadoProcesoProduccion"/> del proceso.</summary>
    public ICommand VerDetalleCommand { get; }

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
        [.. _etapas.GetActivas().OrderBy(e => e.Nombre)];

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

    /// <summary>
    /// Un solo botón para las dos transiciones que puede necesitar un proceso en curso: cierra la
    /// etapa actual (si la había, con su resultado y consumo/merma) y, según lo que el propio
    /// modal decida (<see cref="EtapaProcesoEditorViewModel.ContinuaOtraEtapa"/>), pasa a otra
    /// etapa o termina el proceso — no hay un botón "Terminar" aparte ni una confirmación en un
    /// paso separado.
    /// </summary>
    private void ContinuarProceso()
    {
        if (SelectedItem is not { } proceso)
            return;

        var editor = new EtapaProcesoEditorViewModel(proceso, ListaEtapasActivas(), ListaTipos(), ListaArticulos());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => editor.ContinuaOtraEtapa
            ? _servicio.AgregarEtapa(proceso, editor.ObtenerResultado(), editor.EtapaSeleccionada!,
                                     _sesionActual.UsuarioActual?.Id ?? 0)
            : _servicio.Terminar(proceso, editor.CantidadProducidaValor, editor.ObtenerResultado(),
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

    private void VerDetalle()
    {
        if (SelectedItem is not { } proceso)
            return;

        _dialogos.MostrarEditor(new ProcesoDetalleViewModel(proceso));
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

/// <summary>
/// El único modal para avanzar un proceso: cierra la etapa en curso (si la había, con su
/// resultado y consumo/merma) y, según <see cref="ContinuaOtraEtapa"/> —una elección DENTRO del
/// mismo modal, no un botón aparte—, pasa a otra etapa o termina el proceso. No hay un botón
/// "Terminar" independiente ni una confirmación en un paso separado: es la misma acción,
/// mismo idioma que ya usa <see cref="EntradaEditorViewModel"/>/<see cref="RecepcionMateriaPrimaEditorViewModel"/>
/// para "un formulario, varios modos" con un campo que decide qué mostrar.
///
/// Tiene DOS secciones independientes de esa elección:
/// 1. "Cómo salió la etapa en curso" (resultado, consumo/merma, observaciones) — solo aparece si
///    <see cref="ProcesoProduccion.EtapaActualId"/> no es nulo, es decir, si de verdad hay algo que
///    cerrar. Nunca pregunta por una etapa que recién va a empezar.
/// 2. Lo propio de la elección: a qué etapa nueva pasa, o la cantidad producida si termina.
/// </summary>
public sealed class EtapaProcesoEditorViewModel : CrudEditorViewModelBase<EtapaProcesoProduccion>
{
    private readonly string _numero;
    private readonly string _productoTexto;
    private readonly bool _muestraCierreEtapaActual;
    private readonly string _etapaActualNombre;

    public EtapaProcesoEditorViewModel(ProcesoProduccion proceso,
                                       IReadOnlyList<EtapaProduccion> etapas,
                                       IReadOnlyList<TipoMateriaPrima> tiposMateriaPrima,
                                       IReadOnlyList<Articulo> articulos)
    {
        _numero = proceso.Numero;
        _productoTexto = $"{proceso.ProductoNombre} — planeado {proceso.CantidadPlaneadaTexto}";
        _muestraCierreEtapaActual = proceso.EtapaActualId is not null;
        _etapaActualNombre = proceso.EtapaActualNombre;

        Etapas = etapas;
        TiposMateriaPrima = tiposMateriaPrima;
        Articulos = articulos;

        CantidadProducida = proceso.CantidadPlaneada > 0 ? proceso.CantidadPlaneada.ToString("0.####") : string.Empty;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaConsumoEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    /// <summary>Título y texto del botón reaccionan a <see cref="ContinuaOtraEtapa"/>: cambian en
    /// caliente al tocar el interruptor, no quedan fijos desde que se abrió el modal.</summary>
    public override string Titulo => ContinuaOtraEtapa ? $"Agregar etapa al proceso {_numero}" : $"Terminar proceso {_numero}";

    /// <summary>Amplio: lleva una grilla de líneas dentro, en los dos modos.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => ContinuaOtraEtapa ? "Agregar etapa" : "Terminar proceso";

    /// <summary>Solo se usa cuando no continúa a otra etapa; se muestra en vez del selector de etapa.</summary>
    public string ProductoTexto => _productoTexto;

    /// <summary>
    /// La elección que reemplaza los dos botones de antes ("Agregar etapa"/"Terminar"): vive
    /// DENTRO del modal, no afuera. Por defecto continúa a otra etapa, el camino más frecuente.
    /// </summary>
    private bool _continuaOtraEtapa = true;
    public bool ContinuaOtraEtapa
    {
        get => _continuaOtraEtapa;
        set
        {
            // Notifica todas: Titulo/TextoAccion/MuestraSelectorEtapa/MuestraCantidadProducida
            // dependen de este campo: enumerar un OnPropertyChanged por cada una es la lista que
            // se queda corta el día que se agregue una más — mismo criterio que
            // EntradaEditorViewModel.Tipo.
            if (SetProperty(ref _continuaOtraEtapa, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    /// <summary>Las dos pestañas, enlazadas en DOS VÍAS al <c>IsChecked</c> de su botón, igual que
    /// <see cref="EntradaEditorViewModel.EsCompraProveedor"/>. El setter solo actúa al marcar: al
    /// desmarcar ya hay otro botón del grupo encendiéndose.</summary>
    public bool EsSiguienteEtapa
    {
        get => ContinuaOtraEtapa;
        set { if (value) ContinuaOtraEtapa = true; }
    }

    public bool EsTerminar
    {
        get => !ContinuaOtraEtapa;
        set { if (value) ContinuaOtraEtapa = false; }
    }

    public bool MuestraSelectorEtapa => ContinuaOtraEtapa;
    public bool MuestraCantidadProducida => !ContinuaOtraEtapa;

    /// <summary>Si hay que preguntar "cómo salió" antes de continuar: solo cuando el proceso de
    /// verdad está atravesando una etapa sin confirmar todavía. Si es la primera etapa del
    /// proceso (o ya se cerró la última pendiente), no hay nada que cerrar y esta sección no se
    /// muestra, elija lo que elija en <see cref="ContinuaOtraEtapa"/>.</summary>
    public bool MuestraCierreEtapaActual => _muestraCierreEtapaActual;

    public string EtapaActualNombre => _etapaActualNombre;

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

    /// <summary>Cómo confirma el usuario que salió esta etapa (o el cierre). No obliga a tener una
    /// línea de Merma —ver el comentario de <see cref="Models.ResultadoEtapa"/>—, así que no hay
    /// regla en <see cref="Validar"/> que los relacione.</summary>
    private ResultadoEtapa _resultadoSeleccionado = ResultadoEtapa.Normal;
    public ResultadoEtapa ResultadoSeleccionado
    {
        get => _resultadoSeleccionado;
        set => SetProperty(ref _resultadoSeleccionado, value);
    }

    private string _cantidadProducida = string.Empty;
    public string CantidadProducida
    {
        get => _cantidadProducida;
        set => SetProperty(ref _cantidadProducida, value);
    }

    public bool CantidadProducidaEsValida =>
        !string.IsNullOrWhiteSpace(CantidadProducida) && decimal.TryParse(CantidadProducida, out var v) && v > 0;

    public decimal CantidadProducidaValor => decimal.TryParse(CantidadProducida, out var valor) ? valor : 0m;

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    protected override bool Validar(out string? error)
    {
        if (ContinuaOtraEtapa)
        {
            if (EtapaSeleccionada is null)
            {
                error = "Seleccione la etapa a la que pasa el proceso.";
                return false;
            }
        }
        else if (!CantidadProducidaEsValida)
        {
            error = "Indique cuánto se produjo, con un número mayor que cero. Puede diferir de lo " +
                    "planeado: una merma es justo el dato que interesa registrar.";
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

    /// <summary>
    /// Esto es la CONFIRMACIÓN de cómo salió la etapa en curso (si la había) — nunca la etapa
    /// nueva a la que se pasa. Por eso no lleva <c>EtapaProduccionId</c>/<c>Nombre</c>: el servicio
    /// los estampa a partir de <see cref="ProcesoProduccion.EtapaActualId"/>/<c>EtapaActualNombre</c>
    /// al cerrar (ver <c>ProcesosProduccionService.CerrarEtapaEnCurso</c>); la etapa nueva viaja
    /// aparte, en <see cref="EtapaSeleccionada"/>, que el llamador lee directamente.
    /// </summary>
    public override EtapaProcesoProduccion ObtenerResultado() => new()
    {
        Observaciones = Observaciones.Trim(),
        Resultado = ResultadoSeleccionado,

        // El consumo de una etapa es opcional: las líneas en blanco no se mandan, y quedarse sin
        // ninguna es válido (etapas como "Reposo" o "Etiquetado" no siempre consumen nada nuevo).
        Lineas = [.. Lineas.Where(l => l.TieneMaterialSeleccionado).Select(l => l.ConstruirLineaDeEtapa()!)]
    };

    private LineaConsumoEditorViewModel NuevaLinea() => new(TiposMateriaPrima, Articulos);
}

/// <summary>
/// La ficha de "ver detalle" de un proceso: solo lectura, se abre con doble clic sobre la fila
/// (ver <c>ProcesosProduccionView.xaml.cs</c>). Expone el <see cref="ProcesoProduccion"/> completo
/// en vez de repetir cada propiedad como envoltorio —no hay nada editable aquí, y el modelo ya
/// trae todos los "…Texto" que hace falta pintar—, mismo criterio no genérico que
/// <see cref="MotivoEditorViewModel"/>.
/// </summary>
public sealed class ProcesoDetalleViewModel : CrudEditorViewModelBase
{
    public ProcesoDetalleViewModel(ProcesoProduccion proceso)
    {
        Proceso = proceso;
    }

    public ProcesoProduccion Proceso { get; }

    public override string Titulo => $"Proceso {Proceso.Numero}";

    /// <summary>Amplio: el historial de etapas necesita espacio.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Cerrar";

    /// <summary>Ficha de solo lectura: no hay nada que cancelar, así que un botón "Cancelar" junto
    /// a "Cerrar" sobraría —ver el comentario de <see cref="CrudEditorViewModelBase.MuestraCancelar"/>.</summary>
    public override bool MuestraCancelar => false;

    /// <summary>Si el proceso está atravesando una etapa ahora mismo, todavía sin confirmar cómo
    /// salió. "Dónde está" el proceso se lee como "en qué etapa va", porque el modelo no tiene un
    /// campo de ubicación física.</summary>
    public bool TieneEtapaActual => Proceso.EtapaActualId is not null;

    public bool TieneEtapas => Proceso.Etapas.Count > 0;

    /// <summary>Si hay algo que mostrar en "Consumo inicial" — lo que se consumió al iniciar el
    /// proceso, antes de la primera etapa. Es la única línea de consumo que no vive dentro de
    /// <see cref="ProcesoProduccion.Etapas"/>, así que necesita su propia sección.</summary>
    public bool TieneLineasIniciales => Proceso.LineasIniciales.Count > 0;

    protected override bool Validar(out string? error)
    {
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

    /// <summary>Solo lo usa <see cref="ConstruirLineaDeEtapa"/>: el consumo inicial (<see cref="ConstruirLineaInicial"/>)
    /// nunca es merma, así que esta propiedad no cambia nada ahí.</summary>
    private MotivoConsumoEtapa _motivoSeleccionado = MotivoConsumoEtapa.Consumo;
    public MotivoConsumoEtapa MotivoSeleccionado
    {
        get => _motivoSeleccionado;
        set => SetProperty(ref _motivoSeleccionado, value);
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
            Cantidad = CantidadValor,
            Motivo = MotivoSeleccionado
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
