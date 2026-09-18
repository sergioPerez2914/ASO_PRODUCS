using System.Collections.Generic;
using System.Linq;

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

    /// <summary>Precio de referencia por unidad, para proponerlo al despachar (el operador puede
    /// cambiarlo por línea). Cero significa "sin precio configurado" — el despacho sigue pidiéndolo
    /// a mano en ese caso.</summary>
    public decimal PrecioUnitario { get; set; }

    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");

    /// <summary>Días que dura el producto desde que se termina el proceso; propone la fecha de
    /// vencimiento del lote. Nulo = no vence.</summary>
    public int? DiasVidaUtil { get; set; }

    public string VidaUtilTexto => DiasVidaUtil is { } dias ? $"{dias} días" : "—";

    /// <summary>
    /// NO se persiste (va con <c>Ignore</c> en el DbContext): depende de dos tablas enteras y el
    /// modelo no tiene acceso a la base. La rellena <c>ProductosService.RellenarExistencias</c>
    /// antes de mostrar la lista, igual que <see cref="TipoMateriaPrima.Existencia"/>.
    /// </summary>
    public decimal Existencia { get; set; }

    /// <summary>Existencia por debajo de la cual el producto se marca en la grilla. Cero significa
    /// "no vigilar" — mismo criterio que <see cref="Articulo.Minimo"/>.</summary>
    public decimal Minimo { get; set; }

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

    // --- Resumen de lotes (derivado, no persistido) ---

    /// <summary>
    /// Cuántos lotes con existencia tiene este producto ahora mismo.
    ///
    /// NO se persiste, igual que <see cref="Existencia"/> y por el mismo motivo: sale de recorrer
    /// procesos y despachos enteros. La rellena <c>ProductosService.RellenarResumenDeLotes</c>
    /// antes de pintar la tarjeta del catálogo. Va con <c>Ignore</c> en el DbContext.
    /// </summary>
    public int LotesConExistencia { get; set; }

    /// <summary>La fecha de vencimiento más próxima entre esos lotes (orden FEFO). Nula si
    /// ninguno vence o si no hay lotes.</summary>
    public DateTime? ProximoVencimiento { get; set; }

    /// <summary>
    /// La línea secundaria de la tarjeta: mínimo, precio y vida útil, pero SOLO los que están
    /// configurados.
    ///
    /// En la tabla cada uno tenía su columna y un "—" decía "esto no está puesto" sin estorbar.
    /// En una tarjeta, en cambio, "mín. — · 0,00 por unidad · —" es una línea entera de ruido:
    /// en este catálogo la mayoría de los productos no tienen ni precio ni vida útil todavía.
    /// Vacía si no hay ninguno, y entonces la tarjeta ni pinta la línea.
    /// </summary>
    public string FichaTexto
    {
        get
        {
            var partes = new List<string>(3);

            if (Minimo > 0)
                partes.Add($"mín. {MinimoTexto}");

            // Cero es "sin precio configurado" (ver PrecioUnitario), no un precio de cero.
            if (PrecioUnitario > 0)
                partes.Add($"{PrecioUnitarioTexto} por {UnidadMedida}".Trim());

            if (DiasVidaUtil is not null)
                partes.Add(VidaUtilTexto);

            return string.Join(" · ", partes);
        }
    }

    /// <summary>
    /// La línea de lotes de la tarjeta: "3 lotes · vence 01/10". Es lo único que se pinta, así
    /// que resuelve aquí los tres casos —sin lotes, uno, varios— en vez de repartir
    /// <c>StringFormat</c> y conversores por el XAML.
    /// </summary>
    public string ResumenLotesTexto
    {
        get
        {
            if (LotesConExistencia == 0)
                return "Sin lotes con existencia";

            var lotes = LotesConExistencia == 1 ? "1 lote" : $"{LotesConExistencia} lotes";

            return ProximoVencimiento is { } vence
                ? $"{lotes} · vence {vence:dd/MM}"
                : lotes;
        }
    }

    // --- Presentación / subproducto de otro producto ---

    /// <summary>Si este producto sale de transformar otro (Mantequilla 200 g sale de Mantequilla).
    /// Nulo = se fabrica desde materia prima, como siempre. Es una receta para PROPONER el
    /// consumo al transformar; el proceso guarda lo que de verdad se consumió.</summary>
    public int? ProductoBaseId { get; set; }

    public string ProductoBaseNombre { get; set; } = string.Empty;  // snapshot

    /// <summary>Cuánto del producto base (en SU unidad) lleva una unidad de este: 0,2 kg de
    /// Mantequilla por cada Mantequilla 200 g.</summary>
    public decimal? CantidadBasePorUnidad { get; set; }

    /// <summary>Empaque y otros materiales por unidad producida (1 pote, 1 etiqueta…).</summary>
    public List<ProductoComponente> Componentes { get; set; } = [];

    public bool EsDerivado => ProductoBaseId is not null;

    public string OrigenTexto => EsDerivado ? $"De {ProductoBaseNombre}" : "—";

    /// <summary>Copia honda: duplica <see cref="Componentes"/> para no compartir la lista con lo
    /// que está en pantalla.</summary>
    public Producto Clonar()
    {
        var copia = (Producto)MemberwiseClone();
        copia.Componentes = Componentes.Select(c => c.Clonar()).ToList();
        return copia;
    }
}

/// <summary>Un material que lleva cada unidad de un producto derivado, además del producto base.
/// Solo materia prima o artículo de inventario.</summary>
public class ProductoComponente
{
    public OrigenMaterial Origen { get; set; }

    public int MaterialId { get; set; }
    public string MaterialNombre { get; set; } = string.Empty;       // snapshot
    public string UnidadMedidaSnapshot { get; set; } = string.Empty; // snapshot

    public decimal CantidadPorUnidad { get; set; }

    public ProductoComponente Clonar() => (ProductoComponente)MemberwiseClone();
}
