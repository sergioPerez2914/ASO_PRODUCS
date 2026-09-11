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
/// Documento de recepción de materia prima: lo que entró, de cada tipo.
///
/// No genera cuenta por pagar ni factura de proveedor: a diferencia de
/// <see cref="EntradaInventario"/>, este módulo no asume que lo recibido se compró — puede ser
/// aporte de un socio, cosecha propia o cualquier otro origen que no pasa por Finanzas. Si el
/// negocio real sí compra la materia prima, esa regla se agrega en <c>RecepcionesMateriaPrimaService</c>
/// siguiendo el ejemplo de <c>EntradasInventarioService</c>.
///
/// Es la raíz de un agregado (ver <see cref="Lineas"/>).
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

    /// <summary>Referencia externa (guía, remito, orden del productor). No es el correlativo
    /// interno; es lo que evita cargar dos veces la misma entrega.</summary>
    public string Referencia { get; set; } = string.Empty;

    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Lo que llegó, tipo de materia prima por tipo.</summary>
    public List<RecepcionMateriaPrimaLinea> Lineas { get; set; } = [];

    public EstadoRecepcionMateriaPrima Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;  // snapshot
    public DateTime FechaCreacion { get; set; }

    /// <summary>Si esta recepción suma a la existencia. Una recepción anulada deja de contar, que
    /// es lo que hace que anular devuelva la existencia sin tocar ninguna otra fila.</summary>
    public bool CuentaEnExistencia => Estado == EstadoRecepcionMateriaPrima.Registrada;

    public string EstadoTexto => Estado == EstadoRecepcionMateriaPrima.Registrada ? "Registrada" : "Anulada";

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

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

    public string CantidadTexto => $"{Cantidad:N2} {UnidadMedidaSnapshot}".Trim();

    public RecepcionMateriaPrimaLinea Clonar() => (RecepcionMateriaPrimaLinea)MemberwiseClone();
}
