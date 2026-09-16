using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Models;

/// <summary>Estados de un pedido. Se persiste como ORDINAL: miembros nuevos al final. Binario,
/// mismo criterio que <see cref="EstadoDespacho"/>: "cuánto se entregó" no es un estado propio,
/// se deriva (ver <see cref="EstadoEntregaTexto"/>).</summary>
public enum EstadoPedido
{
    Registrado,
    Anulado
}

/// <summary>
/// Lo que un cliente pidió, antes de despacharlo. NO reserva existencia: es solo lo que falta
/// entregar. Se cumple con uno o más <see cref="Despacho"/> — cada línea de despacho puede citar
/// el <see cref="PedidoLinea"/> que cumple (<see cref="DespachoLinea.PedidoId"/>), y de ahí sale
/// cuánto lleva despachado cada línea, ver <c>PedidosService.RellenarDespachado</c>.
/// </summary>
public class Pedido : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo interno, "PED-000123". Lo asigna <c>PedidosService</c> al registrar,
    /// no el formulario.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public int ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;  // snapshot

    public string Observaciones { get; set; } = string.Empty;

    public List<PedidoLinea> Lineas { get; set; } = [];

    public decimal Total { get; set; }

    public EstadoPedido Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;  // snapshot
    public DateTime FechaCreacion { get; set; }

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string TotalTexto => Total.ToString("N2");

    /// <summary>
    /// "Completado"/"Parcial"/"Pendiente", derivado de <see cref="PedidoLinea.Despachado"/> —
    /// requiere que <c>PedidosService.RellenarDespachado</c> haya corrido antes, igual que
    /// <see cref="Producto.Existencia"/> necesita su propio relleno antes de leerse.
    /// </summary>
    public string EstadoEntregaTexto =>
        Lineas.Count > 0 && Lineas.All(l => l.Pendiente <= 0)
            ? "Completado"
            : Lineas.Any(l => l.Despachado > 0)
                ? "Parcial"
                : "Pendiente";

    /// <summary>Una sola cadena para pintar el chip: "Anulado" gana sobre cualquier avance de
    /// entrega — un pedido anulado no vuelve a mostrarse como Pendiente/Parcial.</summary>
    public string EstadoMostrado => Estado == EstadoPedido.Anulado ? "Anulado" : EstadoEntregaTexto;

    /// <summary>
    /// Copia HONDA: duplica <see cref="Lineas"/> de verdad, mismo motivo que
    /// <see cref="Despacho.Clonar"/>.
    /// </summary>
    public Pedido Clonar()
    {
        var copia = (Pedido)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Renglón de lo pedido. <see cref="Despachado"/> NO se persiste (va con <c>Ignore</c> en el
/// DbContext): depende de la tabla de despachos entera, y la rellena
/// <c>PedidosService.RellenarDespachado</c> antes de mostrar el pedido, igual que
/// <see cref="Producto.Existencia"/>.
/// </summary>
public class PedidoLinea
{
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;      // snapshot
    public string UnidadMedidaSnapshot { get; set; } = string.Empty; // snapshot

    public decimal CantidadPedida { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }

    public decimal Despachado { get; set; }

    public decimal Pendiente => CantidadPedida - Despachado;

    public bool EstaCompleta => Pendiente <= 0;

    public string CantidadPedidaTexto => $"{CantidadPedida:N2} {UnidadMedidaSnapshot}".Trim();
    public string DespachadoTexto => $"{Despachado:N2} {UnidadMedidaSnapshot}".Trim();
    public string PendienteTexto => $"{Pendiente:N2} {UnidadMedidaSnapshot}".Trim();
    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string SubtotalTexto => Subtotal.ToString("N2");

    public PedidoLinea Clonar() => (PedidoLinea)MemberwiseClone();
}
