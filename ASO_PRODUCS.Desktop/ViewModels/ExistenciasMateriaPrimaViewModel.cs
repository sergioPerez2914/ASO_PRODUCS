using System;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Materia Prima · Existencias: el catálogo de tipos de materia prima con su existencia al día.
///
/// Mismo arquetipo que <see cref="AlmacenViewModel"/>: es el maestro CRUD del módulo (aquí se dan
/// de alta los tipos) con una vuelta — la existencia no está en la tabla, se calcula de
/// Recepciones menos Salidas. Como los modelos no avisan de sus cambios, cada vez que se rellena
/// hay que refrescar la vista a mano — ver <see cref="Recargar"/>.
/// </summary>
public sealed class ExistenciasMateriaPrimaViewModel : PantallaCrudViewModel<TipoMateriaPrima, int>
{
    private const string FiltroTodos = "Todos";

    private readonly MateriaPrimaService _servicio;

    private string _filtro = FiltroTodos;

    public ExistenciasMateriaPrimaViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearTiposMateriaPrima(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ExistenciasMateriaPrimaViewModel(Modulo modulo,
                                             Submodulo submodulo,
                                             ITipoMateriaPrimaDataSource tipos,
                                             IServicioDialogo dialogos,
                                             ISesionActual sesion)
        : base(modulo, submodulo, tipos, dialogos, sesion)
    {
        _servicio = new MateriaPrimaService(tipos,
                                            DataSourceFactory.CrearRecepcionesMateriaPrima(),
                                            DataSourceFactory.CrearSalidasMateriaPrima());

        // La base ya pobló Items en su constructor, pero sin existencias: sin esto la primera
        // pintada saldría con todo en cero hasta la primera recarga.
        _servicio.RellenarExistencias(Items);
        ItemsView.Refresh();

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });
    }

    public ICommand CambiarFiltroCommand { get; }

    public string Resumen =>
        $"{Items.Count(t => t.Activo)} tipos activos · {Items.Count(t => t.SinExistencia)} sin existencia";

    protected override string ModuloPermiso => "TiposMateriaPrima";

    protected override bool CoincideBusqueda(TipoMateriaPrima item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(TipoMateriaPrima item) => _filtro switch
    {
        "Con existencia" => item.Existencia > 0,
        "Sin existencia" => item.SinExistencia,
        "Inactivos" => !item.Activo,
        _ => true
    };

    /// <summary>Un tipo que ya se movió no se borra, se desactiva.</summary>
    protected override bool PuedeEliminar(TipoMateriaPrima item) => _servicio.PuedeEliminar(item);

    protected override TipoMateriaPrima CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<TipoMateriaPrima> CrearEditor(TipoMateriaPrima item) =>
        new TipoMateriaPrimaEditorViewModel(item, _servicio);

    /// <summary>
    /// Relee el catálogo y le vuelve a pegar la existencia encima. El refresco va después de
    /// rellenar y no antes: el filtro "sin existencia" depende justamente de ese número.
    /// </summary>
    public override void Recargar()
    {
        base.Recargar();

        _servicio.RellenarExistencias(Items);
        ItemsView.Refresh();
        OnTodasLasPropiedadesCambiaron();
    }
}

/// <summary>Alta/edición de un tipo de materia prima.</summary>
public sealed class TipoMateriaPrimaEditorViewModel : CrudEditorViewModelBase<TipoMateriaPrima>
{
    private readonly TipoMateriaPrima _original;
    private readonly MateriaPrimaService _servicio;

    public TipoMateriaPrimaEditorViewModel(TipoMateriaPrima original, MateriaPrimaService servicio)
    {
        _original = original;
        _servicio = servicio;

        Nombre = original.Nombre;
        UnidadMedida = original.UnidadMedida;
        Activo = original.Id == 0 || original.Activo;
    }

    public override string Titulo =>
        _original.Id == 0 ? "Nuevo tipo de materia prima" : $"Editar {_original.Nombre}";

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

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error) => _servicio.Validar(ObtenerResultado(), out error);

    public override TipoMateriaPrima ObtenerResultado()
    {
        var tipo = _original.Clonar();
        tipo.Nombre = Nombre.Trim();
        tipo.UnidadMedida = UnidadMedida.Trim();
        tipo.Activo = Activo;
        return tipo;
    }
}
