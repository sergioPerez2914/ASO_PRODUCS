using System.Collections.Generic;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Facturas de venta (Cuentas por Cobrar). La implementa
/// una fuente EF Core; la interfaz mantiene la UI y los ViewModels ajenos a la persistencia.
/// </summary>
public interface IFacturaClienteDataSource : ICrudDataSource<FacturaCliente, int>
{
    IEnumerable<FacturaCliente> GetByCliente(int clienteId);
}
