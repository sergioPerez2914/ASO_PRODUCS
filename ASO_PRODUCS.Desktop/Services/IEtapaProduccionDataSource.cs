using System.Collections.Generic;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Catálogo de etapas de producción. La implementa una fuente EF Core; la interfaz mantiene la
/// UI y los ViewModels ajenos a la persistencia.
/// </summary>
public interface IEtapaProduccionDataSource : ICrudDataSource<EtapaProduccion, int>
{
    /// <summary>Lo que se puede elegir hoy al agregar una etapa a un proceso: las dadas de baja
    /// siguen en el historial pero no se ofrecen en el selector.</summary>
    IEnumerable<EtapaProduccion> GetActivas();
}
