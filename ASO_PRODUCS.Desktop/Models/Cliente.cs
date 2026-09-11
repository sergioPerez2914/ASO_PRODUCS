namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Cliente al que se le despacha producto terminado. Dato maestro de la organización.
/// </summary>
public class Cliente : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Registro de información fiscal (RIF o cédula, según el cliente sea empresa o persona).</summary>
    public string Rif { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;
    public string Notas { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";

    public string Etiqueta => string.IsNullOrWhiteSpace(Rif) ? Nombre : $"{Nombre} · {Rif}";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original en la lista.</summary>
    public Cliente Clonar() => (Cliente)MemberwiseClone();
}
