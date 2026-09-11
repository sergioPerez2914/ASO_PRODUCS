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

    // --- Resúmenes para el panel del módulo ---

    public int TotalActivas() => _etapas.GetAll().Count(e => e.Activo);
}
