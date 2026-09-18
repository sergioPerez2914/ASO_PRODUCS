using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Cómo se pone en papel cada documento del negocio.
///
/// Las seis conversiones viven juntas y no repartidas por sus ViewModels por el mismo motivo que
/// <see cref="ExportadorExcel"/> es uno solo: son la misma decisión —qué datos del documento
/// entran en el papel y en qué orden— tomada seis veces, y tenerlas a la vista es lo que hace
/// que un boleto de salida y una recepción se parezcan entre sí.
///
/// <b>Todo sale de las propiedades "…Texto" de los modelos</b>, que es donde ya se decidió cómo
/// se escribe un monto, una fecha o una cantidad con su unidad. Imprimir no vuelve a formatear
/// nada: si la pantalla dice "12,50 Kg", el papel dice exactamente eso.
/// </summary>
public static class DocumentosImprimibles
{
    private const string FirmaRecibido = "Recibí conforme (nombre, cédula y firma)";
    private const string FirmaEntregado = "Entregado por";

    public static DocumentoImprimible De(SalidaInventario salida) => new(
        "Boleto de salida",
        salida.Numero,
        [
            ("Fecha", salida.FechaTexto),
            ("Motivo", salida.MotivoDetalleTexto),
            ("Retirado por", salida.RetiradoPor),
            ("Autorizado por", salida.AutorizadoPorNombre),
            ("Estado", salida.EstadoTexto),
            ("Observaciones", salida.Observaciones),
        ],
        ["Artículo", "Cantidad"],
        [.. salida.Lineas.Select(l => (IReadOnlyList<string>)[l.ArticuloTexto, l.CantidadTexto])],
        [("Total de unidades", salida.TotalUnidades.ToString("N2"))],
        FirmaRecibido);

    public static DocumentoImprimible De(EntradaInventario entrada) => new(
        "Entrada de almacén",
        entrada.Numero,
        [
            ("Fecha", entrada.FechaTexto),
            ("Tipo", entrada.TipoTexto),
            ("Origen", entrada.OrigenTexto),
            ("Documento", entrada.NumeroDocumento),
            ("Cuenta por pagar", entrada.CuentaPorPagarTexto),
            ("Estado", entrada.EstadoTexto),
            ("Observaciones", entrada.Observaciones),
        ],
        ["Artículo", "Cantidad", "Precio", "Subtotal"],
        [.. entrada.Lineas.Select(l => (IReadOnlyList<string>)
            [l.ArticuloTexto, l.CantidadTexto, l.PrecioUnitarioTexto, l.SubtotalTexto])],
        [("Total", entrada.TotalTexto)],
        FirmaEntregado);

    public static DocumentoImprimible De(RecepcionMateriaPrima recepcion) => new(
        "Recepción de materia prima",
        recepcion.Numero,
        [
            ("Fecha", recepcion.FechaTexto),
            ("Tipo", recepcion.TipoTexto),
            ("Origen", recepcion.OrigenTexto),
            ("Documento", recepcion.NumeroDocumento),
            ("Referencia", recepcion.Referencia),
            ("Recibido por", recepcion.RecibidoPor),
            ("Estado", recepcion.EstadoTexto),
            ("Observaciones", recepcion.Observaciones),
        ],
        ["Materia prima", "Cantidad", "Precio", "Subtotal"],
        [.. recepcion.Lineas.Select(l => (IReadOnlyList<string>)
            [l.TipoMateriaPrimaNombre, l.CantidadTexto, l.PrecioUnitarioTexto, l.SubtotalTexto])],
        [("Total", recepcion.TotalTexto)],
        FirmaEntregado);

    public static DocumentoImprimible De(SalidaMateriaPrima salida) => new(
        "Salida de materia prima",
        salida.Numero,
        [
            ("Fecha", salida.FechaTexto),
            ("Motivo", salida.MotivoDetalleTexto),
            ("Estado", salida.EstadoTexto),
            ("Observaciones", salida.Observaciones),
        ],
        ["Materia prima", "Cantidad"],
        [.. salida.Lineas.Select(l => (IReadOnlyList<string>)[l.TipoMateriaPrimaNombre, l.CantidadTexto])],
        [("Total de unidades", salida.TotalCantidad.ToString("N2"))],
        FirmaRecibido);

    public static DocumentoImprimible De(Despacho despacho) => new(
        "Despacho",
        despacho.Numero,
        [
            ("Fecha", despacho.FechaTexto),
            ("Tipo", despacho.TipoTexto),
            ("Cliente", despacho.ClienteNombre),
            ("Autorizado por", despacho.AutorizadoPorNombre),
            ("Cuenta por cobrar", despacho.CuentaPorCobrarTexto),
            ("Estado", despacho.EstadoTexto),
            ("Observaciones", despacho.Observaciones),
        ],
        // El lote va en el papel: es lo que permite rastrear una devolución hasta el proceso que
        // lo produjo, y es obligatorio en cada línea desde que existen lotes y vencimientos.
        ["Producto", "Lote", "Cantidad", "Precio", "Subtotal"],
        [.. despacho.Lineas.Select(l => (IReadOnlyList<string>)
            [l.ProductoNombre, l.ProcesoProduccionNumero, l.CantidadTexto,
             l.PrecioUnitarioTexto, l.SubtotalTexto])],
        [("Total de unidades", despacho.TotalCantidad.ToString("N2")), ("Total", despacho.TotalTexto)],
        FirmaRecibido);

    public static DocumentoImprimible De(Pedido pedido) => new(
        "Pedido",
        pedido.Numero,
        [
            ("Fecha", pedido.FechaTexto),
            ("Cliente", pedido.ClienteNombre),
            ("Estado", pedido.EstadoMostrado),
            ("Observaciones", pedido.Observaciones),
        ],
        // Pedido y Pendiente en el mismo papel: un pedido impreso se usa para preparar la
        // entrega, y lo que hace falta preparar es lo que falta, no lo que se pidió en total.
        ["Producto", "Pedido", "Despachado", "Pendiente", "Precio", "Subtotal"],
        [.. pedido.Lineas.Select(l => (IReadOnlyList<string>)
            [l.ProductoNombre, l.CantidadPedidaTexto, l.DespachadoTexto, l.PendienteTexto,
             l.PrecioUnitarioTexto, l.SubtotalTexto])],
        [("Total", pedido.TotalTexto)],
        null);
}
