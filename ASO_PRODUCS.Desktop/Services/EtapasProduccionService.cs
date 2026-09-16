using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas del catálogo de etapas de producción.
/// </summary>
public sealed class EtapasProduccionService
{
    private readonly IEtapaProduccionDataSource _etapas;
    private readonly IProcesoProduccionDataSource _procesos;

    public EtapasProduccionService(IEtapaProduccionDataSource etapas, IProcesoProduccionDataSource procesos)
    {
        _etapas = etapas;
        _procesos = procesos;
    }

    // --- Validación del maestro ---

    public bool Validar(EtapaProduccion etapa, out string? error)
    {
        if (string.IsNullOrWhiteSpace(etapa.Nombre))
        {
            error = "Indique el nombre de la etapa.";
            return false;
        }

        var repetida = _etapas.GetAll()
            .Where(e => e.Id != etapa.Id)
            .Any(e => string.Equals(e.Nombre.Trim(), etapa.Nombre.Trim(), System.StringComparison.OrdinalIgnoreCase));

        if (repetida)
        {
            error = $"Ya hay una etapa llamada {etapa.Nombre.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Una etapa que ya se usó en algún proceso no se borra: se desactiva. Borrarla dejaría el
    /// historial de ese proceso citando una etapa que no existe. Cuenta cualquier proceso, sin
    /// importar su estado — un proceso anulado también es historial.
    /// </summary>
    public bool PuedeEliminar(EtapaProduccion etapa)
        => !_procesos.GetAll().Any(p => p.Etapas.Any(e => e.EtapaProduccionId == etapa.Id));

    /// <summary>
    /// Da de alta, de una sola vez, las etapas sugeridas que todavía no existan por nombre
    /// (reutiliza <see cref="Validar"/>, que ya rechaza las repetidas). Pensado para precargar el
    /// catálogo de una planta nueva; correrlo más de una vez no duplica nada. Devuelve cuántas se
    /// crearon.
    /// </summary>
    public int CargarSugeridas(IEnumerable<(string Nombre, string Descripcion)> sugeridas)
    {
        var creadas = 0;

        foreach (var (nombre, descripcion) in sugeridas)
        {
            var candidata = new EtapaProduccion { Nombre = nombre, Descripcion = descripcion, Activo = true };

            if (!Validar(candidata, out _))
                continue;

            _etapas.Add(candidata);
            creadas++;
        }

        return creadas;
    }

    // --- Resúmenes para el panel del módulo ---

    public int TotalActivas() => _etapas.GetAll().Count(e => e.Activo);
}
