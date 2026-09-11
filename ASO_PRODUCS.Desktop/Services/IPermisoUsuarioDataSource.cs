using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>Ajustes de permisos por usuario, administrados dentro de la organizacion activa.</summary>
public interface IPermisoUsuarioDataSource : ICrudDataSource<PermisoUsuario, int>
{
}
