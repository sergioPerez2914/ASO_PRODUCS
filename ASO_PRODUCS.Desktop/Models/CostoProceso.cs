using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Costo de materiales de un proceso de producción. NO se persiste: lo calcula
/// <c>CostosProduccionService</c> cada vez, igual que las existencias derivadas.
/// </summary>
public sealed class CostoProceso
{
    public int ProcesoId { get; init; }

    public IReadOnlyList<LineaCostoProceso> Lineas { get; init; } = [];

    /// <summary>Nulo mientras el proceso no está Terminado.</summary>
    public decimal? CantidadProducida { get; init; }

    public string UnidadMedida { get; init; } = string.Empty;

    /// <summary>Precio ACTUAL del producto, no el que tenía cuando se fabricó. Cero = sin precio.</summary>
    public decimal PrecioVenta { get; init; }

    public decimal CostoTotal => Lineas.Sum(l => l.Costo);

    public decimal CostoMerma => Lineas.Where(l => l.EsMerma).Sum(l => l.Costo);

    /// <summary>Algún material no tiene compras con precio (o sale de un lote cuyo costo ya era
    /// incompleto): el costo real es mayor al calculado.</summary>
    public bool Incompleto => Lineas.Any(l => l.CostoUnitario is null || l.CostoParcial);

    public decimal? CostoPorUnidad => CantidadProducida is > 0 ? CostoTotal / CantidadProducida.Value : null;

    public decimal? Margen => CostoPorUnidad is { } costo && PrecioVenta > 0 ? PrecioVenta - costo : null;

    public decimal? MargenPorcentaje => Margen is { } margen ? margen / PrecioVenta * 100 : null;

    public string CostoTotalTexto => CostoTotal.ToString("N2");
    public string CostoMermaTexto => CostoMerma.ToString("N2");
    public string CostoPorUnidadTexto => CostoPorUnidad is { } c ? c.ToString("N2") : "—";
    public string PrecioVentaTexto => PrecioVenta > 0 ? PrecioVenta.ToString("N2") : "—";
    public string MargenTexto => Margen is { } m ? m.ToString("N2") : "—";
    public string MargenPorcentajeTexto => MargenPorcentaje is { } p ? $"{p:N1}%" : "—";
}

/// <summary>Un material consumido por el proceso, valorado a su costo promedio a la fecha del consumo.</summary>
public sealed class LineaCostoProceso
{
    public OrigenMaterial Origen { get; init; }
    public string MaterialNombre { get; init; } = string.Empty;
    public string Unidad { get; init; } = string.Empty;
    public decimal Cantidad { get; init; }
    public bool EsMerma { get; init; }

    /// <summary>Nulo si el material no tiene ninguna compra con precio hasta esa fecha.</summary>
    public decimal? CostoUnitario { get; init; }

    /// <summary>Solo para un lote de producto: su costo por unidad salió de un cálculo incompleto.</summary>
    public bool CostoParcial { get; init; }

    public string? LoteNumero { get; init; }

    public decimal Costo => Cantidad * (CostoUnitario ?? 0);

    public string OrigenTexto => OrigenMaterialTexto.De(Origen, LoteNumero);
    public string CantidadTexto => $"{Cantidad:N2} {Unidad}".Trim();
    public string CostoUnitarioTexto => CostoUnitario is { } c ? c.ToString("N2") : "Sin costo";
    public string CostoTexto => CostoUnitario is null ? "—" : Costo.ToString("N2");
    public string MotivoTexto => EsMerma ? "Merma/exceso" : "Consumo";
}
