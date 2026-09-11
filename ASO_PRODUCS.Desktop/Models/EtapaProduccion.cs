namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Catálogo de etapas que puede atravesar un proceso de producción (lavado, cuajado, prensado,
/// salado, maduración…). Dato maestro de la organización, reutilizable entre procesos: un
/// <see cref="ProcesoProduccion"/> no escribe el nombre de su etapa a mano, elige de aquí.
/// </summary>
public class EtapaProduccion : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Orden sugerido al listar el catálogo en el selector; no obliga a nada, un proceso
    /// puede agregar sus etapas en cualquier orden real.</summary>
    public int Orden { get; set; }

    public bool Activo { get; set; } = true;

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original
    /// en la lista.</summary>
    public EtapaProduccion Clonar() => (EtapaProduccion)MemberwiseClone();
}
