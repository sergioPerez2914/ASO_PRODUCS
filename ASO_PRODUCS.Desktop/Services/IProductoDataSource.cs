using System.Collections.Generic;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Catálogo de productos terminados. La implementa una fuente EF Core; la interfaz mantiene la
/// UI y los ViewModels ajenos a la persistencia.
/// </summary>
public interface IProductoDataSource : ICrudDataSource<Producto, int>
{
    /// <summary>Lo que se puede elegir hoy al iniciar un proceso o registrar un despacho: los
    /// productos dados de baja siguen en el historial pero no se ofrecen en los formularios.</summary>
    IEnumerable<Producto> GetActivos();
}
