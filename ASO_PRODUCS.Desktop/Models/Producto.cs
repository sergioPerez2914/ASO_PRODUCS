namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Catálogo de productos terminados: lo que fabrica Procesos · Producción y lo que despacha
/// Procesos · Despacho. Dato maestro de la organización.
///
/// No guarda su existencia. La existencia se DERIVA (lo producido por los procesos Terminados,
/// menos lo despachado, sin contar documentos anulados) por el mismo motivo que
/// <see cref="TipoMateriaPrima.Existencia"/>: un número editable a mano se desincroniza del
/// historial. Ver <c>ProductosService.ExistenciasPorProducto</c>.
/// </summary>
public class Producto : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Unidad en que se cuenta este producto (kg, unidades, cajas…). Texto libre: mismo
    /// criterio que <see cref="TipoMateriaPrima.UnidadMedida"/>.</summary>
    public string UnidadMedida { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    /// <summary>
    /// NO se persiste (va con <c>Ignore</c> en el DbContext): depende de dos tablas enteras y el
    /// modelo no tiene acceso a la base. La rellena <c>ProductosService.RellenarExistencias</c>
    /// antes de mostrar la lista, igual que <see cref="TipoMateriaPrima.Existencia"/>.
    /// </summary>
    public decimal Existencia { get; set; }

    public bool SinExistencia => Activo && Existencia <= 0;

    public string EstadoTexto => !Activo ? "Inactivo" : SinExistencia ? "Sin existencia" : "Disponible";

    public string ExistenciaTexto => $"{Existencia:N2} {UnidadMedida}".Trim();

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original
    /// en la lista.</summary>
    public Producto Clonar() => (Producto)MemberwiseClone();
}
