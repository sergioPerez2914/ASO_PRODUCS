using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Padron de nucleos que usan ASO. No lleva filtro por organizacion — es la tabla que
/// DEFINE el ambito — asi que quien la consulte debe exigir el permiso correspondiente;
/// hoy solo la usa el selector del Desarrollador.
/// </summary>
public interface IOrganizacionDataSource : ICrudDataSource<Organizacion, int>
{
}
