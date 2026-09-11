using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas de las salidas de materia prima.
///
/// Más simple que su contraparte de recepciones: una salida no le debe nada a nadie, así que no
/// arrastra ningún documento de otro módulo. Lo único que no puede pasar es que la existencia
/// quede en negativo, y esa es la regla que este servicio defiende.
/// </summary>
public sealed class SalidasMateriaPrimaService
{
    private const string Prefijo = "SMP-";

    private readonly ISalidaMateriaPrimaDataSource _salidas;
    private readonly MateriaPrimaService _materiaPrima;
    private readonly ISesionActual _sesion;

    public SalidasMateriaPrimaService(ISalidaMateriaPrimaDataSource salidas,
                                      MateriaPrimaService materiaPrima,
                                      ISesionActual sesion)
    {
        _salidas = salidas;
        _materiaPrima = materiaPrima;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    public bool PuedeAnular(SalidaMateriaPrima salida) => salida.Estado == EstadoSalidaMateriaPrima.Registrada;

    // --- Validación ---

    /// <summary>
    /// Valida la salida contra la existencia REAL del momento, no contra la que tenía el
    /// formulario al abrirse: entre que se abre el modal y se guarda, otro puesto pudo haber
    /// despachado. La pantalla avisa antes, pero quien manda es esta comprobación.
    /// </summary>
    public bool Validar(SalidaMateriaPrima salida, out string? error)
    {
        if (salida.Lineas.Count == 0)
        {
            error = "Agregue al menos un tipo de materia prima a la salida.";
            return false;
        }

        if (salida.Lineas.Any(l => l.TipoMateriaPrimaId == 0))
        {
            error = "Hay una línea sin tipo de materia prima seleccionado.";
            return false;
        }

        if (salida.Lineas.Any(l => l.Cantidad <= 0))
        {
            error = "La cantidad de cada línea debe ser mayor que cero.";
            return false;
        }

        var existencias = _materiaPrima.ExistenciasPorTipo();

        // Se agrupa por tipo antes de comparar: dos líneas del mismo tipo que por separado caben,
        // juntas pueden no caber.
        foreach (var grupo in salida.Lineas.GroupBy(l => l.TipoMateriaPrimaId))
        {
            var pedido = grupo.Sum(l => l.Cantidad);
            var disponible = existencias.GetValueOrDefault(grupo.Key);

            if (pedido > disponible)
            {
                var linea = grupo.First();
                error = $"No hay existencia suficiente de {linea.TipoMateriaPrimaNombre}: " +
                        $"hay {disponible:N2} y se piden {pedido:N2}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    // --- Transiciones ---

    /// <summary>
    /// Emite la salida. Quién autoriza no es un campo del formulario: es quien tiene la sesión
    /// abierta, y se estampa aquí para que no se pueda escribir otro nombre.
    /// </summary>
    public SalidaMateriaPrima Registrar(SalidaMateriaPrima salida, int usuarioId)
    {
        if (salida.Id != 0)
            throw new InvalidOperationException("Esta salida ya está emitida.");

        if (!_sesion.Puede(Permisos.SalidasMateriaPrima.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar salidas de materia prima.");

        if (!Validar(salida, out var error))
            throw new InvalidOperationException(error);

        salida.Numero = SiguienteNumero();
        salida.Estado = EstadoSalidaMateriaPrima.Registrada;
        salida.AutorizadoPorId = usuarioId;
        salida.AutorizadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        salida.CreadoPorId = usuarioId;
        salida.FechaCreacion = DateTime.Now;

        return _salidas.Add(salida);
    }

    /// <summary>
    /// Anula la salida. La existencia vuelve sola: el kardex no cuenta los documentos anulados,
    /// así que no hay que devolver nada a mano.
    /// </summary>
    public SalidaMateriaPrima Anular(SalidaMateriaPrima salida, string motivo)
    {
        if (!PuedeAnular(salida))
            throw new InvalidOperationException("Solo se puede anular una salida registrada.");

        if (!_sesion.Puede(Permisos.SalidasMateriaPrima.Anular))
            throw new InvalidOperationException("No tienes permiso para anular salidas de materia prima.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var copia = salida.Clonar();
        copia.Estado = EstadoSalidaMateriaPrima.Anulada;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _salidas.Update(copia);
        return copia;
    }

    // --- Piezas internas ---

    /// <summary>
    /// Correlativo interno. Mismo criterio que el de las recepciones: se calcula al emitir y el
    /// índice único <c>(OrganizacionId, Numero)</c> es la red por si dos puestos coincidieran.
    /// </summary>
    private string SiguienteNumero()
    {
        var ultimo = _salidas.GetAll()
            .Select(s => s.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    // --- Resúmenes para el panel del módulo ---

    public IReadOnlyList<SalidaMateriaPrima> DelMes()
    {
        var desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        return [.. _salidas.GetAll()
            .Where(s => s.Estado == EstadoSalidaMateriaPrima.Registrada && s.Fecha.Date >= desde)];
    }
}
