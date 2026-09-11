namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Identidad mínima de una entidad maestra, usada por el esqueleto CRUD genérico
/// para localizar el ítem seleccionado en la grilla.
/// </summary>
public interface IEntidad<TId>
{
    TId Id { get; }
}
