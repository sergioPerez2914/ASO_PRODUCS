using System.Collections.Generic;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Catálogo de tipos de materia prima. La implementa una fuente EF Core; la interfaz mantiene la
/// UI y los ViewModels ajenos a la persistencia.
/// </summary>
public interface ITipoMateriaPrimaDataSource : ICrudDataSource<TipoMateriaPrima, int>
{
    /// <summary>Lo que se puede elegir hoy en una recepción o una salida: los tipos dados de
    /// baja siguen en el historial pero no se ofrecen en los formularios.</summary>
    IEnumerable<TipoMateriaPrima> GetActivos();
}
