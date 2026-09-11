using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Boletos de salida del almacén con sus líneas. No agrega consultas propias: el kardex recorre
/// el listado completo y no hay pantalla que filtre por artículo.
/// </summary>
public interface ISalidaInventarioDataSource : ICrudDataSource<SalidaInventario, int>
{
}
