using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Pedidos de clientes con sus líneas. La implementa una fuente EF Core; la interfaz mantiene la
/// UI y los ViewModels ajenos a la persistencia.
/// </summary>
public interface IPedidoDataSource : ICrudDataSource<Pedido, int>
{
}
