using System;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Finanzas · Clientes: maestro de clientes, calco de <see cref="ProveedoresViewModel"/>. Listado
/// único, sin conmutador de padrones.
/// </summary>
public sealed class ClientesViewModel : PantallaCrudViewModel<Cliente, int>
{
    private const string FiltroTodos = "Todos";

    private readonly IClienteDataSource _clientes;
    private readonly ClientesService _servicio;

    private string _filtro = FiltroTodos;

    public ClientesViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearClientes(), new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private ClientesViewModel(Modulo modulo,
                              Submodulo submodulo,
                              IClienteDataSource clientes,
                              IServicioDialogo dialogos,
                              ISesionActual sesion)
        : base(modulo, submodulo, clientes, dialogos, sesion)
    {
        _clientes = clientes;
        _servicio = new ClientesService(DataSourceFactory.CrearFacturasCliente(),
                                        DataSourceFactory.CrearDespachos(),
                                        DataSourceFactory.CrearPedidos());

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });
    }

    public ICommand CambiarFiltroCommand { get; }

    public string Resumen => $"{Items.Count(c => c.Activo)} clientes activos de {Items.Count}";

    protected override string ModuloPermiso => "Clientes";

    protected override string Describir(Cliente item) => item.Nombre;

    protected override bool CoincideBusqueda(Cliente item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Rif.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Telefono.Contains(texto, StringComparison.OrdinalIgnoreCase);

    /// <summary>Ver <see cref="ProveedoresViewModel.PasaFiltroExtra"/>: la columna "Estado" estaba
    /// y no había con qué filtrarla.</summary>
    protected override bool PasaFiltroExtra(Cliente item) => _filtro switch
    {
        "Activos" => item.Activo,
        "Inactivos" => !item.Activo,
        _ => true
    };

    /// <summary>Un cliente que ya aparece en un documento no se borra, se desactiva.</summary>
    protected override bool PuedeEliminar(Cliente item) => _servicio.PuedeEliminar(item);

    protected override Cliente CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Cliente> CrearEditor(Cliente item) =>
        new ClienteEditorViewModel(item, _clientes);
}
