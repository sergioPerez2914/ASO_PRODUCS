namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Catálogo de tipos de materia prima: qué se recibe y qué se despacha. Dato maestro de la
/// organización.
///
/// Es uno de los dos <see cref="IMaterialMaestro"/> del proyecto —el otro es
/// <see cref="Articulo"/>— y los dos se dan de alta con el MISMO formulario.
///
/// No guarda su existencia. La existencia se DERIVA (lo que entró menos lo que salió, sin contar
/// documentos anulados) por el mismo motivo que <see cref="Articulo.Existencia"/>: un número
/// editable a mano se desincroniza del historial. Ver <see cref="Existencia"/>.
/// </summary>
public class TipoMateriaPrima : IEntidad<int>, IDeOrganizacion, IMaterialMaestro
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>
    /// Código con el que la organización identifica el tipo. Único dentro de la organización. Si
    /// quien lo da de alta no escribe uno, <c>MateriaPrimaService.GenerarCodigo</c> le pone uno
    /// aleatorio y legible — calco de <see cref="Articulo.Codigo"/>.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Unidad en que se cuenta este tipo (kg, L, sacos…). Texto libre con una lista
    /// sugerida (<see cref="Configuration.UnidadesSugeridas"/>): mientras no haya una necesidad
    /// real de un catálogo cerrado, no vale la pena mantenerlo.</summary>
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

    /// <summary>
    /// Precio promedio ponderado de las compras de este tipo. Tampoco se persiste, y por el mismo
    /// motivo que <see cref="Existencia"/>: sale del historial de recepciones. La rellena
    /// <c>MateriaPrimaService.RellenarPrecios</c> antes de mostrar la lista.
    ///
    /// Cero significa "no hay ninguna compra con precio" (un tipo que solo entró por recepciones
    /// de otro origen), no "sale gratis": por eso <see cref="PrecioPromedioTexto"/> lo pinta
    /// como "—".
    /// </summary>
    public decimal PrecioPromedio { get; set; }

    public string PrecioPromedioTexto => PrecioPromedio > 0 ? PrecioPromedio.ToString("N2") : "—";

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

    public string Etiqueta => $"{Codigo} · {Nombre}";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original
    /// en la lista.</summary>
    public TipoMateriaPrima Clonar() => (TipoMateriaPrima)MemberwiseClone();
}
