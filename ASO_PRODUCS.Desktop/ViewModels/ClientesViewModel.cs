using System;
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
    private readonly IClienteDataSource _clientes;

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
    }

    protected override string ModuloPermiso => "Clientes";

    protected override bool CoincideBusqueda(Cliente item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Rif.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Telefono.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override Cliente CrearNuevo() => new() { Activo = true };

    protected override CrudEditorViewModelBase<Cliente> CrearEditor(Cliente item) =>
        new ClienteEditorViewModel(item, _clientes);
}
