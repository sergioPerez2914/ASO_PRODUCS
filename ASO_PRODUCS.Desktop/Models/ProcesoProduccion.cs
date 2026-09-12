using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Models;

/// <summary>Estados de un proceso de producción. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum EstadoProcesoProduccion
{
    EnProceso,
    Terminado,
    Anulado
}

/// <summary>
/// De dónde sale el material que consume una línea de un proceso: del padrón de materia prima o
/// del de artículos de inventario, indistintamente. Se persiste como ORDINAL: miembros nuevos al
/// final.
/// </summary>
public enum OrigenMaterial
{
    MateriaPrima,
    Articulo
}

/// <summary>Cómo terminó una etapa. Se persiste como ORDINAL: miembros nuevos al final.</summary>
public enum ResultadoEtapa
{
    Normal,
    ConIncidencia,
    ConMerma
}

/// <summary>
/// Por qué se consumió una línea dentro de una etapa: lo normal para esa etapa, o una
/// merma/exceso sobre lo habitual. Distinto de <see cref="MotivoSalida"/>/
/// <see cref="MotivoSalidaMateriaPrima"/> (esos son del documento de salida completo; este es por
/// línea, dentro de una misma etapa). Se persiste como ORDINAL: miembros nuevos al final.
/// </summary>
public enum MotivoConsumoEtapa
{
    Consumo,
    Merma
}

/// <summary>
/// Documento de fabricación: consume materia prima y/o artículos de inventario para producir un
/// producto terminado, a lo largo de las etapas que la planta necesite.
///
/// Es la raíz de un agregado de DOS niveles (<see cref="LineasIniciales"/> y
/// <see cref="Etapas"/>, y cada etapa con sus propias <see cref="EtapaProcesoProduccion.Lineas"/>).
/// Cada línea de consumo, inicial o de etapa, genera una salida real y numerada en Materia Prima o
/// en Inventario (según su <see cref="OrigenMaterial"/>) — ver <c>ProcesosProduccionService</c>.
/// </summary>
public class ProcesoProduccion : IEntidad<int>, IDeOrganizacion
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>Correlativo interno, "PRO-000123". Lo asigna
    /// <c>ProcesosProduccionService</c> al iniciar, no el formulario.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public int ProductoId { get; set; }

    public string ProductoNombre { get; set; } = string.Empty;  // snapshot

    public string UnidadMedidaSnapshot { get; set; } = string.Empty;  // snapshot

    public decimal CantidadPlaneada { get; set; }

    /// <summary>Cuánto se obtuvo de verdad. Nulo hasta <c>ProcesosProduccionService.Terminar</c>;
    /// puede diferir de <see cref="CantidadPlaneada"/> por una merma, y eso no es un error — es el
    /// dato que interesa registrar.</summary>
    public decimal? CantidadProducida { get; set; }

    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Lo que se consumió al iniciar el proceso, antes de la primera etapa.</summary>
    public List<ProcesoProduccionLineaInicial> LineasIniciales { get; set; } = [];

    /// <summary>Las etapas que fue atravesando el proceso, en el orden en que se agregaron. Solo
    /// contiene etapas ya CERRADAS/confirmadas —la que está en curso ahora mismo, sin confirmar
    /// todavía, vive en <see cref="EtapaActualId"/>, no acá.</summary>
    public List<EtapaProcesoProduccion> Etapas { get; set; } = [];

    /// <summary>La etapa que el proceso está atravesando ahora mismo, todavía sin confirmar cómo
    /// salió. Nulo si el proceso nunca entró a ninguna etapa, o si ya se cerró la última pendiente
    /// (Terminar la deja en <c>null</c>). Se confirma —resultado, merma/exceso, observaciones— justo
    /// antes de pasar a la siguiente etapa o de terminar el proceso; ver
    /// <c>ProcesosProduccionService.AgregarEtapa</c>/<c>Terminar</c>.</summary>
    public int? EtapaActualId { get; set; }

    public string EtapaActualNombre { get; set; } = string.Empty;  // snapshot

    public EstadoProcesoProduccion Estado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }

    public int CreadoPorId { get; set; }
    public string CreadoPorNombre { get; set; } = string.Empty;  // snapshot
    public DateTime FechaCreacion { get; set; }

    public int? TerminadoPorId { get; set; }
    public string TerminadoPorNombre { get; set; } = string.Empty;  // snapshot
    public DateTime? FechaTermino { get; set; }

    /// <summary>Si este proceso suma a la existencia del producto. Solo un proceso Terminado
    /// cuenta; uno anulado —haya o no llegado a Terminado— deja de contar, que es lo que hace que
    /// anular devuelva la existencia del producto sin tocar ninguna otra fila.</summary>
    public bool CuentaEnExistencia => Estado == EstadoProcesoProduccion.Terminado;

    /// <summary>En curso, muestra el nombre de la etapa actual (o "En proceso" si todavía no entró
    /// a ninguna) en vez del texto genérico.</summary>
    public string EstadoTexto => Estado switch
    {
        EstadoProcesoProduccion.EnProceso => EtapaActualId is not null ? EtapaActualNombre : "En proceso",
        EstadoProcesoProduccion.Terminado => "Terminado",
        _ => "Anulado"
    };

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");

    public string CantidadPlaneadaTexto => $"{CantidadPlaneada:N2} {UnidadMedidaSnapshot}".Trim();

    public string CantidadProducidaTexto => CantidadProducida is { } cantidad
        ? $"{cantidad:N2} {UnidadMedidaSnapshot}".Trim()
        : "—";

    public int CantidadEtapas => Etapas.Count;

    /// <summary>
    /// Copia HONDA: duplica de verdad <see cref="LineasIniciales"/>, <see cref="Etapas"/> y, dentro
    /// de cada etapa, sus propias líneas. Un <c>MemberwiseClone</c> superficial compartiría esas
    /// listas con lo que está en pantalla, y <c>AgregarEtapa</c>/<c>Anular</c> mutarían el original
    /// al tocar la copia.
    /// </summary>
    public ProcesoProduccion Clonar()
    {
        var copia = (ProcesoProduccion)MemberwiseClone();
        copia.LineasIniciales = LineasIniciales.Select(l => l.Clonar()).ToList();
        copia.Etapas = Etapas.Select(e => e.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Renglón de consumo inicial (tab "Iniciar"). Guarda el <c>MaterialId</c> —en el espacio de ids
/// que le corresponda según <see cref="Origen"/>— para poder generar la salida correspondiente, y
/// el nombre y la unidad como snapshot, para que el documento siga leyéndose igual aunque el
/// tipo/artículo cambie de nombre después.
/// </summary>
public class ProcesoProduccionLineaInicial
{
    public OrigenMaterial Origen { get; set; }

    public int MaterialId { get; set; }
    public string MaterialNombre { get; set; } = string.Empty;      // snapshot
    public string UnidadMedidaSnapshot { get; set; } = string.Empty; // snapshot

    public decimal Cantidad { get; set; }

    public string CantidadTexto => $"{Cantidad:N2} {UnidadMedidaSnapshot}".Trim();

    public string OrigenTexto => Origen == OrigenMaterial.MateriaPrima ? "Materia prima" : "Inventario";

    public ProcesoProduccionLineaInicial Clonar() => (ProcesoProduccionLineaInicial)MemberwiseClone();
}

/// <summary>
/// Una etapa ya CERRADA de un proceso: el registro queda cuando se confirma cómo salió, justo
/// antes de pasar a la siguiente etapa o de terminar el proceso (ver
/// <c>ProcesosProduccionService.AgregarEtapa</c>/<c>Terminar</c>) — no se edita ni se quita
/// después. Mientras la etapa sigue en curso y sin confirmar, no existe todavía como
/// <see cref="EtapaProcesoProduccion"/>: solo es <see cref="ProcesoProduccion.EtapaActualId"/>.
/// </summary>
public class EtapaProcesoProduccion
{
    public int EtapaProduccionId { get; set; }
    public string EtapaProduccionNombre { get; set; } = string.Empty;  // snapshot

    public string Observaciones { get; set; } = string.Empty;

    public DateTime FechaRegistro { get; set; }

    /// <summary>Cómo confirmó el usuario que salió esta etapa. No obliga a tener una línea con
    /// <see cref="MotivoConsumoEtapa.Merma"/> ni al revés — el resultado puede quedar explicado
    /// solo en <see cref="Observaciones"/>.</summary>
    public ResultadoEtapa Resultado { get; set; }

    /// <summary>Consumo/merma de esta etapa, confirmado al cerrarla: el material que hizo falta y,
    /// si lo hubo, lo que se perdió o se consumió de más. Opcional: quedarse sin ninguna línea es
    /// válido.</summary>
    public List<EtapaProcesoProduccionLinea> Lineas { get; set; } = [];

    public string FechaRegistroTexto => FechaRegistro.ToString("dd/MM/yyyy HH:mm");

    public string ResultadoTexto => Resultado switch
    {
        ResultadoEtapa.Normal => "Normal",
        ResultadoEtapa.ConIncidencia => "Con incidencia",
        _ => "Con merma"
    };

    public int CantidadLineas => Lineas.Count;

    /// <summary>Copia honda: duplica también <see cref="Lineas"/>, mismo motivo que
    /// <see cref="ProcesoProduccion.Clonar"/>.</summary>
    public EtapaProcesoProduccion Clonar()
    {
        var copia = (EtapaProcesoProduccion)MemberwiseClone();
        copia.Lineas = Lineas.Select(l => l.Clonar()).ToList();
        return copia;
    }
}

/// <summary>
/// Renglón de consumo de una etapa. Mismo shape que <see cref="ProcesoProduccionLineaInicial"/>;
/// es una clase separada porque cada una es su propia tabla owned con su propia clave foránea
/// (a la etapa una, a la línea inicial a la raíz), igual criterio que
/// <see cref="RecepcionMateriaPrimaLinea"/>/<see cref="SalidaMateriaPrimaLinea"/>.
/// </summary>
public class EtapaProcesoProduccionLinea
{
    public OrigenMaterial Origen { get; set; }

    public int MaterialId { get; set; }
    public string MaterialNombre { get; set; } = string.Empty;      // snapshot
    public string UnidadMedidaSnapshot { get; set; } = string.Empty; // snapshot

    public decimal Cantidad { get; set; }

    /// <summary>Si esta línea es el consumo normal de la etapa o una merma/exceso sobre lo
    /// habitual. Por defecto (ordinal 0) es <see cref="MotivoConsumoEtapa.Consumo"/>, para que
    /// nada existente cambie de comportamiento.</summary>
    public MotivoConsumoEtapa Motivo { get; set; }

    public string CantidadTexto => $"{Cantidad:N2} {UnidadMedidaSnapshot}".Trim();

    public string OrigenTexto => Origen == OrigenMaterial.MateriaPrima ? "Materia prima" : "Inventario";

    public string MotivoTexto => Motivo == MotivoConsumoEtapa.Consumo ? "Consumo" : "Merma/exceso";

    public EtapaProcesoProduccionLinea Clonar() => (EtapaProcesoProduccionLinea)MemberwiseClone();
}
