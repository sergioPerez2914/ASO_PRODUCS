using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Procesos de producción con sus líneas iniciales y sus etapas (cada una con las suyas propias).
/// La implementa una fuente EF Core; la interfaz mantiene la UI y los ViewModels ajenos a la
/// persistencia.
/// </summary>
public interface IProcesoProduccionDataSource : ICrudDataSource<ProcesoProduccion, int>
{
}
