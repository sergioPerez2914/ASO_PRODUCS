using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// La única regla de dominio que les faltaba a los dos padrones de terceros: no borrar a alguien
/// que ya aparece en un documento.
///
/// <para><b>Por qué hacía falta.</b> Proveedores y Clientes eran los dos únicos maestros sin
/// servicio de dominio y sin redefinir <c>PuedeEliminar</c>, así que heredaban el <c>true</c> de
/// <c>CrudViewModelBase</c> y se podían borrar siempre. Como el modelo no tiene claves foráneas
/// reales (ver <c>BD/DbContext.cs</c>), la base no lo impedía: borrar un proveedor con facturas
/// no fallaba, simplemente dejaba las facturas apuntando a un Id que ya no existe. Lo único que
/// sobrevivía era el nombre en el snapshot de texto de cada documento, que es suficiente para
/// leerlos pero no para volver a pagarle.</para>
///
/// <para>Lo que sí se puede hacer con un tercero que dejó de operar es <b>desactivarlo</b>
/// (<c>Activo = false</c>): deja de ofrecerse en los editores y el historial queda intacto. Es
/// el mismo criterio que ya aplican Almacén y Existencias con un artículo que se movió.</para>
///
/// <para>Los dos van en un archivo porque son la misma regla escrita dos veces contra padrones
/// distintos; separarlos serían dos archivos de quince líneas que nadie lee por separado.</para>
/// </summary>
public sealed class ProveedoresService
{
    private readonly IFacturaProveedorDataSource _facturas;
    private readonly IEntradaInventarioDataSource _entradas;
    private readonly IRecepcionMateriaPrimaDataSource _recepciones;

    public ProveedoresService(IFacturaProveedorDataSource facturas,
                              IEntradaInventarioDataSource entradas,
                              IRecepcionMateriaPrimaDataSource recepciones)
    {
        _facturas = facturas;
        _entradas = entradas;
        _recepciones = recepciones;
    }

    /// <summary>
    /// Los tres sitios donde un proveedor deja rastro: la factura que se le debe, la entrada de
    /// almacén que se le compró y la recepción de materia prima que se le recibió. Cuenta también
    /// los documentos anulados: anular no deshace que ese tercero existió en el historial.
    /// </summary>
    public bool PuedeEliminar(Proveedor proveedor)
        => !_facturas.GetAll().Any(f => f.ProveedorId == proveedor.Id)
           && !_entradas.GetAll().Any(e => e.ProveedorId == proveedor.Id)
           && !_recepciones.GetAll().Any(r => r.ProveedorId == proveedor.Id);
}

/// <summary>El gemelo de <see cref="ProveedoresService"/> del lado del cobro. Ver allá el porqué.</summary>
public sealed class ClientesService
{
    private readonly IFacturaClienteDataSource _facturas;
    private readonly IDespachoDataSource _despachos;
    private readonly IPedidoDataSource _pedidos;

    public ClientesService(IFacturaClienteDataSource facturas,
                           IDespachoDataSource despachos,
                           IPedidoDataSource pedidos)
    {
        _facturas = facturas;
        _despachos = despachos;
        _pedidos = pedidos;
    }

    public bool PuedeEliminar(Cliente cliente)
        => !_facturas.GetAll().Any(f => f.ClienteId == cliente.Id)
           && !_despachos.GetAll().Any(d => d.ClienteId == cliente.Id)
           && !_pedidos.GetAll().Any(p => p.ClienteId == cliente.Id);
}
