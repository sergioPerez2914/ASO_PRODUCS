using System;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Finanzas · Proveedores: maestro de proveedores, promovido a submódulo propio (antes era la
/// pestaña "Proveedores" dentro de Cuentas por Pagar). Listado único, sin conmutador de padrones,
/// mismo arquetipo que <see cref="ClientesViewModel"/>.
/// </summary>
public sealed class ProveedoresViewModel : PantallaCrudViewModel<Proveedor, int>
{
    private const string FiltroTodos = "Todos";

    private readonly IProveedorDataSource _proveedores;
    private readonly ProveedoresService _servicio;

    private string _filtro = FiltroTodos;

    public ProveedoresViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearProveedores(), new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ProveedoresViewModel(Modulo modulo,
                                 Submodulo submodulo,
                                 IProveedorDataSource proveedores,
                                 IServicioDialogo dialogos,
                                 ISesionActual sesion)
        : base(modulo, submodulo, proveedores, dialogos, sesion)
    {
        _proveedores = proveedores;
        _servicio = new ProveedoresService(DataSourceFactory.CrearFacturasProveedor(),
                                           DataSourceFactory.CrearEntradasInventario(),
                                           DataSourceFactory.CrearRecepcionesMateriaPrima());

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });
    }

    public ICommand CambiarFiltroCommand { get; }

    /// <summary>El padrón de un vistazo, igual que en Almacén y Existencias.</summary>
    public string Resumen => $"{Items.Count(p => p.Activo)} proveedores activos de {Items.Count}";

    protected override string ModuloPermiso => "Proveedores";

    protected override string Describir(Proveedor item) => item.Nombre;

    protected override bool CoincideBusqueda(Proveedor item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Rif.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Telefono.Contains(texto, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// La columna "Estado" estaba desde siempre, pero no había con qué filtrarla: un padrón con
    /// años de proveedores dados de baja se leía mezclado con los que se usan hoy.
    /// </summary>
    protected override bool PasaFiltroExtra(Proveedor item) => _filtro switch
    {
        "Activos" => item.Activo,
        "Inactivos" => !item.Activo,
        _ => true
    };

    /// <summary>Un proveedor que ya aparece en un documento no se borra, se desactiva.</summary>
    protected override bool PuedeEliminar(Proveedor item) => _servicio.PuedeEliminar(item);

    protected override Proveedor CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Proveedor> CrearEditor(Proveedor item) =>
        new ProveedorEditorViewModel(item, _proveedores);
}
