using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Models;

/// <summary>Estados de una salida. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoSalidaMateriaPrima
{
    Registrada,
    Anulada
}

/// <summary>Por qué salió esta materia prima. Espejo de <see cref="MotivoSalida"/> (el de
/// Inventario), sin <c>Devolucion</c>/<c>Traslado</c> porque no aplican aquí todavía. Se persiste
/// como ORDINAL: miembros nuevos al final.</summary>
public enum MotivoSalidaMateriaPrima
{
    Consumo,
    Merma
}

/// <summary>
/// Documento de salida de materia prima: lo que salió, de cada tipo.
///
/// No tiene documento aguas abajo, y tampoco hace falta un área de destino aquí — si el negocio
/// real necesita registrar a quién o a dónde va la materia prima, agregar ese campo cuando se
/// conozca la necesidad real. Anularla solo devuelve la existencia, porque el kardex no cuenta lo
/// anulado.
///
/// Es la raíz de un agregado (ver <see cref="Lineas"/>).
/// </summary>
public class SalidaMateriaPrima : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo interno, "SMP-000123". Lo asigna
    /// <c>SalidasMateriaPrimaService</c> al registrar, no el formulario.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Por qué salió. Por defecto (ordinal 0) es <see cref="MotivoSalidaMateriaPrima.Consumo"/>,
    /// para que una salida registrada a mano (que nunca fija este campo) siga leyéndose igual que
    /// antes de que existiera.</summary>
    public MotivoSalidaMateriaPrima Motivo { get; set; }

    /// <summary>Proceso de producción que originó esta salida, si vino de un consumo de
    /// Procesos y no de un registro manual. Enlace suelto, sin clave foránea real, igual que
    /// <see cref="EntradaInventario.FacturaProveedorId"/>.</summary>
    public int? ProcesoProduccionId { get; set; }

    public string ProcesoProduccionNumero { get; set; } = string.Empty;  // snapshot

    /// <summary>Lo que salió, tipo de materia prima por tipo.</summary>
    public List<SalidaMateriaPrimaLinea> Lineas { get; set; } = [];

    public EstadoSalidaMateriaPrima Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    /// <summary>Quien tenía la sesión abierta al emitir la salida. No es un campo del
    /// formulario: lo pone el servicio a partir de la sesión.</summary>
    public int AutorizadoPorId { get; set; }
    public string AutorizadoPorNombre { get; set; } = string.Empty;  // snapshot

    public int CreadoPorId { get; set; }
    public DateTime FechaCreacion { get; set; }

    /// <summary>Si esta salida resta de la existencia. Una salida anulada deja de contar, que es
    /// lo que hace que anular devuelva la existencia sin tocar ninguna otra fila.</summary>
    public bool CuentaEnExistencia => Estado == EstadoSalidaMateriaPrima.Registrada;

    public string EstadoTexto => Estado == EstadoSalidaMateriaPrima.Registrada ? "Registrada" : "Anulada";

    public string MotivoTexto => Motivo == MotivoSalidaMateriaPrima.Consumo ? "Consumo" : "Merma";

    /// <summary>"Consumo"/"Merma" a secas no distingue una salida cargada a mano de una que
    /// generó un proceso de producción — las dos dicen igual hoy. Si vino de un proceso
    /// (<see cref="ProcesoProduccionId"/> no nulo), lo dice.</summary>
    public string MotivoDetalleTexto => ProcesoProduccionId is not null
        ? $"{MotivoTexto} · Proceso {ProcesoProduccionNumero}"
        : MotivoTexto;

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

    public int CantidadLineas => Lineas.Count;

    public decimal TotalCantidad => Lineas.Sum(l => l.Cantidad);

    /// <summary>
    /// Copia con las líneas duplicadas de verdad: <c>MemberwiseClone</c> compartiría la misma
    /// lista entre el original y la copia, y editar la copia mutaría lo que está en pantalla.
    /// </summary>
    public SalidaMateriaPrima Clonar()
    {
        var copia = (SalidaMateriaPrima)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Renglón de una salida. Mismo criterio que <see cref="RecepcionMateriaPrimaLinea"/>: el
/// <c>TipoMateriaPrimaId</c> para la existencia, y el nombre congelado como snapshot.
/// </summary>
public class SalidaMateriaPrimaLinea
{
    public int TipoMateriaPrimaId { get; set; }
    public string TipoMateriaPrimaNombre { get; set; } = string.Empty;  // snapshot
    public string UnidadMedidaSnapshot { get; set; } = string.Empty;    // snapshot

    public decimal Cantidad { get; set; }

    public string CantidadTexto => $"{Cantidad:N2} {UnidadMedidaSnapshot}".Trim();

    public SalidaMateriaPrimaLinea Clonar() => (SalidaMateriaPrimaLinea)MemberwiseClone();
}
