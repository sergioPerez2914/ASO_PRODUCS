using System;

namespace ASO_PRODUCS.Desktop.Models;

public enum EstadoVencimientoLote
{
    SinVencimiento,
    Vigente,
    PorVencer,
    Vencido
}

/// <summary>
/// Un lote de producto terminado = un proceso Terminado. NO se persiste: su existencia la deriva
/// <c>ProductosService.Lotes</c> (lo producido menos lo despachado de ese lote).
/// </summary>
public sealed class LoteProducto
{
    public const int DiasPorVencer = 7;

    public int ProcesoId { get; init; }
    public string Numero { get; init; } = string.Empty;
    public int ProductoId { get; init; }
    public string ProductoNombre { get; init; } = string.Empty;
    public string Unidad { get; init; } = string.Empty;
    public DateTime? FechaTermino { get; init; }
    public DateTime? FechaVencimiento { get; init; }
    public decimal Producido { get; init; }
    public decimal Despachado { get; set; }

    /// <summary>Lo que consumieron otros procesos (transformaciones) de este lote.</summary>
    public decimal Transformado { get; set; }

    public decimal Existencia => Producido - Despachado - Transformado;

    public EstadoVencimientoLote Estado => FechaVencimiento is not { } vence
        ? EstadoVencimientoLote.SinVencimiento
        : vence.Date < DateTime.Today
            ? EstadoVencimientoLote.Vencido
            : vence.Date <= DateTime.Today.AddDays(DiasPorVencer)
                ? EstadoVencimientoLote.PorVencer
                : EstadoVencimientoLote.Vigente;

    public string EstadoTexto => Estado switch
    {
        EstadoVencimientoLote.Vencido => "Vencido",
        EstadoVencimientoLote.PorVencer => "Por vencer",
        EstadoVencimientoLote.Vigente => "Vigente",
        _ => "Sin vencimiento"
    };

    public string FechaTerminoTexto => FechaTermino?.ToString("dd/MM/yyyy") ?? "—";
    public string FechaVencimientoTexto => FechaVencimiento?.ToString("dd/MM/yyyy") ?? "—";
    public string ProducidoTexto => $"{Producido:N2} {Unidad}".Trim();
    public string DespachadoTexto => $"{Despachado:N2} {Unidad}".Trim();
    public string TransformadoTexto => Transformado > 0 ? $"{Transformado:N2} {Unidad}".Trim() : "—";
    public string ExistenciaTexto => $"{Existencia:N2} {Unidad}".Trim();

    public string Etiqueta => FechaVencimiento is { } vence
        ? $"{Numero} · vence {vence:dd/MM/yyyy} · {ExistenciaTexto}"
        : $"{Numero} · {ExistenciaTexto}";

    public string EtiquetaConProducto => $"{ProductoNombre} · {Etiqueta}";
}
