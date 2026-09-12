using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Models;

/// <summary>Estados de un despacho. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoDespacho
{
    Registrado,
    Anulado
}

/// <summary>
/// A quién se le despacha y si genera cuenta por cobrar. Se persiste como ORDINAL: miembros
/// nuevos al final. Mismo criterio que <see cref="TipoEntrada"/>: "Ajuste" es el único que no
/// genera un documento de Finanzas — mermas, uso interno, muestras, no hay a quién cobrarle.
/// </summary>
public enum TipoDespacho
{
    Venta,
    Ajuste
}

/// <summary>
/// Documento de despacho de producto terminado: lo que salió, de cada producto, a qué precio y a
/// quién.
///
/// Es la raíz de un agregado (ver <see cref="Lineas"/>) y, cuando es una <see cref="TipoDespacho.Venta"/>,
/// el origen de una cuenta por cobrar: registrarlo crea la <see cref="FacturaCliente"/>
/// correspondiente en Finanzas. El enlace al documento generado se guarda como un <c>int</c>
/// suelto sin clave foránea real, igual que <see cref="EntradaInventario.FacturaProveedorId"/>.
/// Anularlo devuelve la existencia sola, porque el kardex no cuenta lo anulado.
/// </summary>
public class Despacho : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo interno, "DES-000123". Lo asigna
    /// <c>DespachosService</c> al registrar, no el formulario.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public TipoDespacho Tipo { get; set; }

    /// <summary>A quién se le vende. Nulo en un Ajuste.</summary>
    public int? ClienteId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;  // snapshot

    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Lo que salió, producto por producto.</summary>
    public List<DespachoLinea> Lineas { get; set; } = [];

    /// <summary>Suma de los subtotales. Cero en un Ajuste.</summary>
    public decimal Total { get; set; }

    public EstadoDespacho Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    /// <summary>Quien tenía la sesión abierta al emitir el despacho. No es un campo del
    /// formulario: lo pone el servicio a partir de la sesión.</summary>
    public int AutorizadoPorId { get; set; }
    public string AutorizadoPorNombre { get; set; } = string.Empty;  // snapshot

    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    /// <summary>Cuenta por cobrar que generó este despacho, si generó alguna. Enlace suelto, sin
    /// clave foránea real, como el resto de las relaciones entre documentos.</summary>
    public int? FacturaClienteId { get; set; }
    public string FacturaClienteNumero { get; set; } = string.Empty;  // snapshot

    /// <summary>Si este despacho resta de la existencia. Un despacho anulado deja de contar, que
    /// es lo que hace que anular devuelva la existencia sin tocar ninguna otra fila.</summary>
    public bool CuentaEnExistencia => Estado == EstadoDespacho.Registrado;

    /// <summary>Un ajuste no le cobra a nadie; una venta sí.</summary>
    public bool GeneraCuentaPorCobrar => Tipo != TipoDespacho.Ajuste;

    public string TipoTexto => Tipo == TipoDespacho.Venta ? "Venta" : "Ajuste";

    public string EstadoTexto => Estado == EstadoDespacho.Registrado ? "Registrado" : "Anulado";

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

    public string TotalTexto => Total.ToString("N2");

    public string CuentaPorCobrarTexto => string.IsNullOrWhiteSpace(FacturaClienteNumero)
        ? "—"
        : $"Nº {FacturaClienteNumero}";

    public int CantidadLineas => Lineas.Count;

    public decimal TotalCantidad => Lineas.Sum(l => l.Cantidad);

    /// <summary>
    /// Copia con las líneas duplicadas de verdad: <c>MemberwiseClone</c> compartiría la misma
    /// lista entre el original y la copia, y editar la copia mutaría lo que está en pantalla.
    /// </summary>
    public Despacho Clonar()
    {
        var copia = (Despacho)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Renglón de un despacho. Guarda el <c>ProductoId</c> para poder restar la existencia, y además
/// el nombre y la unidad como snapshot, para que el documento siga leyéndose igual aunque el
/// producto cambie de nombre después.
/// </summary>
public class DespachoLinea
{
    public int ProductoId { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;      // snapshot
    public string UnidadMedidaSnapshot { get; set; } = string.Empty; // snapshot

    public decimal Cantidad { get; set; }

    /// <summary>Cero en un Ajuste: ahí no se vendió nada — mismo criterio que
    /// <see cref="EntradaInventarioLinea.PrecioUnitario"/>.</summary>
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal { get; set; }

    /// <summary>
    /// De qué <see cref="ProcesoProduccion"/> salió lo despachado, si el operador lo indicó al
    /// despachar. Nulo si no se especificó — es un dato INFORMATIVO, elegido a mano: no se valida
    /// contra la existencia del proceso ni descuenta nada de él, porque la existencia de
    /// <see cref="Producto"/> sigue siendo un total global derivado, sin manejo de lotes.
    /// </summary>
    public int? ProcesoProduccionId { get; set; }
    public string ProcesoProduccionNumero { get; set; } = string.Empty;  // snapshot

    public string CantidadTexto => $"{Cantidad:N2} {UnidadMedidaSnapshot}".Trim();
    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string SubtotalTexto => Subtotal.ToString("N2");

    public string ProcesoOrigenTexto => string.IsNullOrWhiteSpace(ProcesoProduccionNumero)
        ? "Sin especificar"
        : $"Proceso {ProcesoProduccionNumero}";

    public DespachoLinea Clonar() => (DespachoLinea)MemberwiseClone();
}
