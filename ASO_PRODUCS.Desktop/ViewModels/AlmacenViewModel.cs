using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Inventario · Almacén: el catálogo de artículos con su existencia al día.
///
/// Es un maestro CRUD normal con una vuelta: la existencia no está en la tabla, se calcula del
/// kardex. Como los modelos no avisan de sus cambios, cada vez que se rellena hay que refrescar
/// la vista a mano — ver <see cref="Recargar"/>.
/// </summary>
public sealed class AlmacenViewModel : PantallaCrudViewModel<Articulo, int>
{
    private const string FiltroTodos = "Todos";

    private readonly InventarioService _servicio;
    private readonly CostosMaterialesService _costos;
    private readonly IServicioDialogo _dialogos;

    private string _filtro = FiltroTodos;

    public AlmacenViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearArticulos(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private AlmacenViewModel(Modulo modulo,
                             Submodulo submodulo,
                             IArticuloDataSource articulos,
                             IServicioDialogo dialogos,
                             ISesionActual sesion)
        : base(modulo, submodulo, articulos, dialogos, sesion)
    {
        _servicio = new InventarioService(articulos,
                                          DataSourceFactory.CrearEntradasInventario(),
                                          DataSourceFactory.CrearSalidasInventario());

        _costos = new CostosMaterialesService(DataSourceFactory.CrearRecepcionesMateriaPrima(),
                                              DataSourceFactory.CrearEntradasInventario(),
                                              DataSourceFactory.CrearSalidasMateriaPrima(),
                                              DataSourceFactory.CrearSalidasInventario());

        _dialogos = dialogos;

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

        CargarSugeridosCommand = new RelayCommand(CargarSugeridos, () => sesion.Puede($"{ModuloPermiso}.Crear"));

        VerFichaCommand = new RelayCommand(VerFicha, () => SelectedItem is not null);
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand CargarSugeridosCommand { get; }
    public ICommand VerFichaCommand { get; }

    /// <summary>
    /// Abre la ficha del artículo: su precio promedio, las compras de las que sale y lo que
    /// debería quedar de cada una. Es lo que hace el doble clic sobre la fila, como en Entradas y
    /// Salidas; editar sigue estando en la barra y en el botón de la propia ficha.
    /// </summary>
    private void VerFicha()
    {
        if (SelectedItem is not { } articulo)
            return;

        var ficha = new FichaMaterialViewModel(_costos.Ficha(articulo), EditarCommand.CanExecute(null));

        if (_dialogos.MostrarEditor(ficha) && ficha.QuiereEditar)
            Editar();
    }

    /// <summary>
    /// Precarga el almacén con los insumos sugeridos. Almacén era el único de los tres catálogos
    /// —con Materia Prima · Existencias y Procesos · Productos— que no tenía con qué arrancar, y
    /// dar de alta a mano ocho insumos de limpieza y empaque antes de poder registrar la primera
    /// entrada es justo el trabajo que este botón evita en las otras dos pantallas.
    /// </summary>
    private void CargarSugeridos()
    {
        var creados = _servicio.CargarSugeridos(CatalogoLacteoSugerido.Articulos);
        Recargar();

        Aviso.Mostrar(creados > 0
            ? $"Se agregaron {creados} artículos sugeridos."
            : "Los artículos sugeridos ya estaban todos cargados.");
    }

    /// <summary>Estado del almacén de un vistazo, sin ir al resumen del módulo.</summary>
    public string Resumen =>
        $"{Items.Count(a => a.Activo)} artículos activos · {Items.Count(a => a.BajoMinimo)} bajo mínimo";

    protected override string ModuloPermiso => "Articulos";

    protected override string Describir(Articulo item) => item.Nombre;

    protected override string NombreDelTipo => "Artículo";

    protected override bool CoincideBusqueda(Articulo item, string texto) =>
        item.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(Articulo item) => _filtro switch
    {
        "Bajo mínimo" => item.BajoMinimo,
        "Sin existencia" => item.SinExistencia,
        "Inactivos" => !item.Activo,
        _ => true
    };

    /// <summary>Un artículo que ya se movió no se borra, se desactiva.</summary>
    protected override bool PuedeEliminar(Articulo item) => _servicio.PuedeEliminar(item);

    protected override Articulo CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Articulo> CrearEditor(Articulo item) =>
        new ArticuloEditorViewModel(item, _servicio);

    /// <summary>
    /// Relee el catálogo y le vuelve a pegar la existencia encima. El refresco va después de
    /// rellenar y no antes: el filtro "bajo mínimo" depende justamente de ese número.
    /// </summary>
    public override void Recargar()
    {
        base.Recargar();

        _servicio.RellenarExistencias(Items);
        _costos.RellenarPrecios(Items);
        ItemsView.Refresh();
        OnTodasLasPropiedadesCambiaron();
    }
}

/// <summary>
/// Alta/edición de un artículo del almacén. Todo el formulario está en
/// <see cref="MaterialEditorViewModel{T}"/>, que comparte con el de materia prima.
/// </summary>
public sealed class ArticuloEditorViewModel(Articulo original, InventarioService servicio)
    : MaterialEditorViewModel<Articulo>(original)
{
    protected override string QueEs => "artículo";

    protected override string GenerarCodigo() => servicio.GenerarCodigo();

    protected override bool ValidarEnServicio(Articulo articulo, out string? error) =>
        servicio.Validar(articulo, out error);

    protected override Articulo Clonar(Articulo original) => original.Clonar();
}
