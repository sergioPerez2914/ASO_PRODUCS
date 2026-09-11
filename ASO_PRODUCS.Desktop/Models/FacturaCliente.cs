using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Estados de una factura de venta. La emite la propia organización, así que —a diferencia de
/// <see cref="FacturaProveedor"/>, que llega ya emitida— existe desde el instante en que se
/// registra. "Vencida" es condición derivada de la fecha (ver <see cref="FacturaCliente.EstaVencida"/>),
/// no un estado guardado.
/// </summary>
public enum EstadoFacturaCliente
{
    Pendiente,
    Cobrada,
    Anulada
}

/// <summary>
/// Factura de un cliente: lo que la organización tiene por cobrar por lo que le vendió.
///
/// A diferencia de <see cref="FacturaProveedor"/> (que no sabe qué la originó, porque
/// <see cref="EntradaInventario"/> guarda el enlace en su propio sentido), esta SÍ guarda el
/// enlace hacia el despacho que la generó, cuando aplica: <c>DespachosService.Anular</c> necesita
/// mirar el estado de la factura antes de decidir si puede anular el despacho.
/// </summary>
public class FacturaCliente : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>En un alta manual, texto libre. Cuando nace de un despacho, el número del
    /// despacho ("DES-000123") — la organización no emite un número de factura aparte.</summary>
    public string NumeroDocumento { get; set; } = string.Empty;

    public int ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;  // snapshot

    public string Descripcion { get; set; } = string.Empty;

    public DateTime FechaEmision { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public decimal Monto { get; set; }

    /// <summary>Detalle de lo que se vendió y su precio — snapshot de texto.</summary>
    public List<FacturaClienteLinea> Lineas { get; set; } = [];

    public EstadoFacturaCliente Estado { get; set; }
    public DateTime? FechaCobro { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    /// <summary>Despacho que originó esta cuenta por cobrar. Nulo en un alta manual.</summary>
    public int? DespachoId { get; set; }
    public string DespachoNumero { get; set; } = string.Empty;  // snapshot

    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    /// <summary>Pendiente, con vencimiento definido y con el plazo cumplido. Es condición
    /// derivada, no un estado guardado.</summary>
    public bool EstaVencida =>
        Estado == EstadoFacturaCliente.Pendiente
        && FechaVencimiento is { } vencimiento
        && vencimiento.Date < DateTime.Today;

    /// <summary>Días de atraso; negativo mientras falte para el vencimiento.</summary>
    public int DiasParaVencer => FechaVencimiento is { } vencimiento
        ? (vencimiento.Date - DateTime.Today).Days
        : 0;

    public string EstadoTexto => EstaVencida
        ? "Vencida"
        : Estado switch
        {
            EstadoFacturaCliente.Pendiente => "Pendiente",
            EstadoFacturaCliente.Cobrada => "Cobrada",
            _ => "Anulada"
        };

    public string MontoTexto => Monto.ToString("N2");

    public string VencimientoTexto => FechaVencimiento is { } vencimiento
        ? vencimiento.ToString("dd/MM/yyyy")
        : "Sin definir";

    public string PlazoTexto => Estado switch
    {
        EstadoFacturaCliente.Cobrada => FechaCobro is { } cobro ? $"Cobrada el {cobro:dd/MM/yyyy}" : "Cobrada",
        EstadoFacturaCliente.Anulada => "Anulada",
        _ when EstaVencida => $"Vencida hace {-DiasParaVencer} día(s)",
        _ => $"Vence en {DiasParaVencer} día(s)"
    };

    public string DespachoTexto => string.IsNullOrWhiteSpace(DespachoNumero) ? "Alta manual" : $"Despacho {DespachoNumero}";

    /// <summary>
    /// Copia con las líneas duplicadas de verdad: <c>MemberwiseClone</c> compartiría la misma
    /// lista entre el original y la copia, y editar la copia mutaría lo que está en pantalla.
    /// </summary>
    public FacturaCliente Clonar()
    {
        var copia = (FacturaCliente)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Línea de una factura de cliente: detalle de lo que se vendió y su precio, snapshot de texto
/// (no referencia ningún catálogo) — mismo shape que <see cref="FacturaProveedorLinea"/>.
/// </summary>
public class FacturaClienteLinea
{
    public string DestinoTexto { get; set; } = string.Empty;
    public string CantidadTexto { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }

    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string SubtotalTexto => Subtotal.ToString("N2");

    public FacturaClienteLinea Clonar() => (FacturaClienteLinea)MemberwiseClone();
}
