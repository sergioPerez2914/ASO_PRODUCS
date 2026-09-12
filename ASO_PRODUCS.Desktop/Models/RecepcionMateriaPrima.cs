using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Models;

/// <summary>Estados de una recepción. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoRecepcionMateriaPrima
{
    Registrada,
    Anulada
}

/// <summary>
/// De dónde vino la materia prima. Espejo de <see cref="TipoEntrada"/> (Inventario), con
/// <see cref="OtroOrigen"/> en vez de un "Ajuste": aquí lo habitual sin proveedor no es corregir
/// un conteo, es un aporte de un socio, cosecha propia, maquila, etc. Las dos primeras generan
/// cuenta por pagar en Finanzas; <see cref="OtroOrigen"/> no, porque no hay a quién deberle. Se
/// persiste como ORDINAL: miembros nuevos al final.
/// </summary>
public enum TipoRecepcionMateriaPrima
{
    CompraProveedor,
    CompraExterna,
    OtroOrigen
}

/// <summary>
/// Documento de recepción de materia prima: lo que entró, de cada tipo, y de dónde vino.
///
/// Es la raíz de un agregado (ver <see cref="Lineas"/>) y, cuando <see cref="GeneraCuentaPorPagar"/>,
/// el origen de una cuenta por pagar: registrarla crea la <see cref="FacturaProveedor"/>
/// correspondiente en Finanzas, salvo cuando es <see cref="TipoRecepcionMateriaPrima.OtroOrigen"/>
/// (aporte de un socio, cosecha propia, maquila...). El enlace al documento generado se guarda
/// como un <c>int</c> suelto sin clave foránea real, igual que <see cref="EntradaInventario.FacturaProveedorId"/>.
/// </summary>
public class RecepcionMateriaPrima : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo interno, "REC-000123". Lo asigna
    /// <c>RecepcionesMateriaPrimaService</c> al registrar, no el formulario.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public TipoRecepcionMateriaPrima Tipo { get; set; }

    /// <summary>
    /// A quién se le compró. Nulo salvo en <see cref="TipoRecepcionMateriaPrima.CompraProveedor"/>.
    /// En una compra externa apunta al comercio/productor, dado de alta al vuelo en el padrón de
    /// proveedores si no existía.
    /// </summary>
    public int? ProveedorId { get; set; }

    public string ProveedorNombre { get; set; } = string.Empty;  // snapshot

    /// <summary>Número de la factura o del recibo que trae el proveedor; no es el correlativo
    /// interno ni <see cref="Referencia"/>. Es lo que evita cargar dos veces la misma compra en
    /// Finanzas.</summary>
    public string NumeroDocumento { get; set; } = string.Empty;

    /// <summary>Plazo de pago que hereda la cuenta por pagar.</summary>
    public DateTime? FechaVencimiento { get; set; }

    /// <summary>Quién de la empresa gestionó la compra. Solo en
    /// <see cref="TipoRecepcionMateriaPrima.CompraExterna"/>.</summary>
    public string RecibidoPor { get; set; } = string.Empty;

    /// <summary>Referencia externa (guía, remito, orden del productor). No es el correlativo
    /// interno; es lo que evita cargar dos veces la misma entrega, independientemente de si
    /// generó o no una cuenta por pagar.</summary>
    public string Referencia { get; set; } = string.Empty;

    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Lo que llegó, tipo de materia prima por tipo.</summary>
    public List<RecepcionMateriaPrimaLinea> Lineas { get; set; } = [];

    /// <summary>Suma de los subtotales. Se guarda porque es lo que se le debe al proveedor y no
    /// debe cambiar si alguien toca un precio después. Cero cuando no hay proveedor.</summary>
    public decimal Total { get; set; }

    public EstadoRecepcionMateriaPrima Estado { get; set; }

    /// <summary>Cuenta por pagar que generó esta recepción, si generó alguna. Enlace suelto, sin
    /// clave foránea real, como el resto de las relaciones entre documentos.</summary>
    public int? FacturaProveedorId { get; set; }

    public string FacturaProveedorNumero { get; set; } = string.Empty;  // snapshot

    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;  // snapshot
    public DateTime FechaCreacion { get; set; }

    /// <summary>Otro origen no le debe nada a nadie; los otros dos tipos sí.</summary>
    public bool GeneraCuentaPorPagar => Tipo != TipoRecepcionMateriaPrima.OtroOrigen;

    /// <summary>Si esta recepción suma a la existencia. Una recepción anulada deja de contar, que
    /// es lo que hace que anular devuelva la existencia sin tocar ninguna otra fila.</summary>
    public bool CuentaEnExistencia => Estado == EstadoRecepcionMateriaPrima.Registrada;

    public string TipoTexto => Tipo switch
    {
        TipoRecepcionMateriaPrima.CompraProveedor => "Compra a proveedor",
        TipoRecepcionMateriaPrima.CompraExterna => "Compra externa",
        _ => "Otro origen"
    };

    public string EstadoTexto => Estado == EstadoRecepcionMateriaPrima.Registrada ? "Registrada" : "Anulada";

    public string OrigenTexto => Tipo switch
    {
        TipoRecepcionMateriaPrima.OtroOrigen => "Aporte, cosecha propia u otro origen",
        TipoRecepcionMateriaPrima.CompraExterna when !string.IsNullOrWhiteSpace(RecibidoPor) =>
            $"{ProveedorNombre} · recibió {RecibidoPor}",
        _ => ProveedorNombre
    };

    public string TotalTexto => Total.ToString("N2");

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

    public string VencimientoTexto => FechaVencimiento is { } vencimiento
        ? vencimiento.ToString("dd/MM/yyyy")
        : "Sin definir";

    public string CuentaPorPagarTexto => string.IsNullOrWhiteSpace(FacturaProveedorNumero)
        ? "—"
        : $"Nº {FacturaProveedorNumero}";

    public int CantidadLineas => Lineas.Count;

    public decimal TotalCantidad => Lineas.Sum(l => l.Cantidad);

    /// <summary>
    /// Copia con las líneas duplicadas de verdad: <c>MemberwiseClone</c> compartiría la misma
    /// lista entre el original y la copia, y editar la copia mutaría lo que está en pantalla.
    /// </summary>
    public RecepcionMateriaPrima Clonar()
    {
        var copia = (RecepcionMateriaPrima)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Renglón de una recepción. Guarda el <c>TipoMateriaPrimaId</c> para poder sumar la existencia,
/// y además el nombre como snapshot, para que el documento siga leyéndose igual aunque el tipo
/// cambie de nombre después.
/// </summary>
public class RecepcionMateriaPrimaLinea
{
    public int TipoMateriaPrimaId { get; set; }
    public string TipoMateriaPrimaNombre { get; set; } = string.Empty;  // snapshot
    public string UnidadMedidaSnapshot { get; set; } = string.Empty;    // snapshot

    public decimal Cantidad { get; set; }

    /// <summary>Cero cuando la recepción no generó cuenta por pagar: ahí no se compró nada.</summary>
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal { get; set; }

    public string CantidadTexto => $"{Cantidad:N2} {UnidadMedidaSnapshot}".Trim();
    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string SubtotalTexto => Subtotal.ToString("N2");

    public RecepcionMateriaPrimaLinea Clonar() => (RecepcionMateriaPrimaLinea)MemberwiseClone();
}
