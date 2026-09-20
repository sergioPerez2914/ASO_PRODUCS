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
    private readonly CostosMaterialesService _costos;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesion;

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
        _costos = new CostosMaterialesService(DataSourceFactory.CrearRecepcionesMateriaPrima(),
                                              DataSourceFactory.CrearEntradasInventario(),
                                              DataSourceFactory.CrearSalidasMateriaPrima(),
                                              DataSourceFactory.CrearSalidasInventario());
        _dialogos = dialogos;
        _sesion = sesion;

        // La base ya pobló Items en su constructor, pero sin existencias ni precios: sin esto la
        // primera pintada saldría con todo en cero hasta la primera recarga.
        _servicio.RellenarExistencias(Items);
        _costos.RellenarPrecios(Items);
        ItemsView.Refresh();

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        CargarSugeridosCommand = new RelayCommand(CargarSugeridos, () => _sesion.Puede($"{ModuloPermiso}.Crear"));

        VerFichaCommand = new RelayCommand(VerFicha, () => SelectedItem is not null);
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand CargarSugeridosCommand { get; }
    public ICommand VerFichaCommand { get; }

    /// <summary>
    /// Abre la ficha del tipo: su precio promedio, las recepciones de las que sale y lo que
    /// debería quedar de cada una. Calco de <see cref="AlmacenViewModel"/> — el doble clic sobre
    /// la fila abre la ficha, no el editor.
    /// </summary>
    private void VerFicha()
    {
        if (SelectedItem is not { } tipo)
            return;

        var ficha = new FichaMaterialViewModel(_costos.Ficha(tipo), EditarCommand.CanExecute(null));

        if (_dialogos.MostrarEditor(ficha) && ficha.QuiereEditar)
            Editar();
    }

    public string Resumen =>
        $"{Items.Count(t => t.Activo)} tipos activos · {Items.Count(t => t.SinExistencia)} sin existencia · " +
        $"{Items.Count(t => t.BajoMinimo)} bajo mínimo";

    protected override string ModuloPermiso => "TiposMateriaPrima";

    protected override string Describir(TipoMateriaPrima item) => item.Nombre;

    protected override string NombreDelTipo => "Tipo de materia prima";

    protected override bool CoincideBusqueda(TipoMateriaPrima item, string texto) =>
        item.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(TipoMateriaPrima item) => _filtro switch
    {
        "Bajo mínimo" => item.BajoMinimo,
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
        _costos.RellenarPrecios(Items);
        ItemsView.Refresh();
        OnTodasLasPropiedadesCambiaron();
    }

    /// <summary>Precarga el catálogo sugerido para una planta láctea; no duplica lo que ya
    /// exista, así que se puede pulsar más de una vez sin riesgo.</summary>
    private void CargarSugeridos()
    {
        var creados = _servicio.CargarSugeridos(CatalogoLacteoSugerido.TiposMateriaPrima);
        Recargar();

        Aviso.Mostrar(creados > 0
            ? $"Se agregaron {creados} tipos de materia prima sugeridos."
            : "Los tipos sugeridos ya estaban todos cargados.");
    }
}

/// <summary>
/// Alta/edición de un tipo de materia prima. Todo el formulario está en
/// <see cref="MaterialEditorViewModel{T}"/>, el mismo que usa el almacén.
/// </summary>
public sealed class TipoMateriaPrimaEditorViewModel(TipoMateriaPrima original, MateriaPrimaService servicio)
    : MaterialEditorViewModel<TipoMateriaPrima>(original)
{
    protected override string QueEs => "tipo de materia prima";

    protected override string GenerarCodigo() => servicio.GenerarCodigo();

    protected override bool ValidarEnServicio(TipoMateriaPrima tipo, out string? error) =>
        servicio.Validar(tipo, out error);

    protected override TipoMateriaPrima Clonar(TipoMateriaPrima original) => original.Clonar();
}
