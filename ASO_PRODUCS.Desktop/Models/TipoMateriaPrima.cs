namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Catálogo de tipos de materia prima: qué se recibe y qué se despacha. Dato maestro de la
/// organización.
///
/// No guarda su existencia. La existencia se DERIVA (lo que entró menos lo que salió, sin contar
/// documentos anulados) por el mismo motivo que <see cref="Articulo.Existencia"/>: un número
/// editable a mano se desincroniza del historial. Ver <see cref="Existencia"/>.
/// </summary>
public class TipoMateriaPrima : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Unidad en que se cuenta este tipo (kg, litros, sacos…). Texto libre: mientras no
    /// haya una necesidad real de un catálogo cerrado, no vale la pena mantenerlo.</summary>
    public string UnidadMedida { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    /// <summary>Existencia por debajo de la cual el tipo se marca en la pantalla. Cero significa
    /// "no vigilar" — mismo criterio que <see cref="Articulo.Minimo"/>.</summary>
    public decimal Minimo { get; set; }

    /// <summary>
    /// NO se persiste (va con <c>Ignore</c> en el DbContext): depende de dos tablas enteras y el
    /// modelo no tiene acceso a la base. La rellena
    /// <c>MateriaPrimaService.RellenarExistencias</c> antes de mostrar la lista, igual que
    /// <see cref="Articulo.Existencia"/>.
    /// </summary>
    public decimal Existencia { get; set; }

    public bool BajoMinimo => Activo && Minimo > 0 && Existencia < Minimo;

    public bool SinExistencia => Activo && Existencia <= 0;

    public string EstadoTexto => !Activo
        ? "Inactivo"
        : SinExistencia
            ? "Sin existencia"
            : BajoMinimo
                ? "Bajo mínimo"
                : "Disponible";

    public string ExistenciaTexto => $"{Existencia:N2} {UnidadMedida}".Trim();

    public string MinimoTexto => Minimo > 0 ? $"{Minimo:N2} {UnidadMedida}".Trim() : "—";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original
    /// en la lista.</summary>
    public TipoMateriaPrima Clonar() => (TipoMateriaPrima)MemberwiseClone();
}
