using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas de las recepciones de materia prima.
///
/// A diferencia de <see cref="EntradasInventarioService"/>, no depende de
/// <see cref="CuentasPorPagarService"/>: este módulo no genera deuda al recibir — si el negocio
/// real sí compra la materia prima, esa dependencia se agrega aquí siguiendo el mismo ejemplo.
/// </summary>
public sealed class RecepcionesMateriaPrimaService
{
    private const string Prefijo = "REC-";

    private readonly IRecepcionMateriaPrimaDataSource _recepciones;
    private readonly MateriaPrimaService _materiaPrima;
    private readonly ISesionActual _sesion;

    public RecepcionesMateriaPrimaService(IRecepcionMateriaPrimaDataSource recepciones,
                                          MateriaPrimaService materiaPrima,
                                          ISesionActual sesion)
    {
        _recepciones = recepciones;
        _materiaPrima = materiaPrima;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    public bool PuedeAnular(RecepcionMateriaPrima recepcion) => recepcion.Estado == EstadoRecepcionMateriaPrima.Registrada;

    // --- Validación ---

    public bool Validar(RecepcionMateriaPrima recepcion, out string? error)
    {
        if (recepcion.Lineas.Count == 0)
        {
            error = "Agregue al menos un tipo de materia prima a la recepción.";
            return false;
        }

        if (recepcion.Lineas.Any(l => l.TipoMateriaPrimaId == 0))
        {
            error = "Hay una línea sin tipo de materia prima seleccionado.";
            return false;
        }

        if (recepcion.Lineas.Any(l => l.Cantidad <= 0))
        {
            error = "La cantidad de cada línea debe ser mayor que cero.";
            return false;
        }

        var repetido = recepcion.Lineas
            .GroupBy(l => l.TipoMateriaPrimaId)
            .FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
        {
            error = $"El tipo {repetido.First().TipoMateriaPrimaNombre} está en más de una línea; " +
                    "júntelas en una sola.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(recepcion.Referencia))
        {
            error = "Indique la referencia de la recepción.";
            return false;
        }

        // Anti-duplicado en la app, sin índice único en BD: mismo criterio que
        // CuentasPorPagarService.Validar con el número de documento del proveedor.
        var referenciaBuscada = recepcion.Referencia.Trim();
        var duplicada = _recepciones.GetAll()
            .Any(r => r.Id != recepcion.Id
                     && r.Estado == EstadoRecepcionMateriaPrima.Registrada
                     && string.Equals(r.Referencia.Trim(), referenciaBuscada, StringComparison.OrdinalIgnoreCase));

        if (duplicada)
        {
            error = $"Ya hay una recepción registrada con la referencia {referenciaBuscada}.";
            return false;
        }

        error = null;
        return true;
    }

    // --- Transiciones ---

    public RecepcionMateriaPrima Registrar(RecepcionMateriaPrima recepcion, int usuarioId)
    {
        if (recepcion.Id != 0)
            throw new InvalidOperationException("Esta recepción ya está registrada.");

        if (!_sesion.Puede(Permisos.RecepcionesMateriaPrima.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar recepciones de materia prima.");

        if (!Validar(recepcion, out var error))
            throw new InvalidOperationException(error);

        recepcion.Numero = SiguienteNumero();
        recepcion.Estado = EstadoRecepcionMateriaPrima.Registrada;
        recepcion.CreadoPorId = usuarioId;
        recepcion.CreadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        recepcion.FechaCreacion = DateTime.Now;

        return _recepciones.Add(recepcion);
    }

    /// <summary>
    /// Deshace la recepción. Lo único que puede impedirlo, y se comprueba antes de escribir
    /// nada: que lo que trajo ya se haya despachado (la existencia quedaría en negativo).
    /// </summary>
    public RecepcionMateriaPrima Anular(RecepcionMateriaPrima recepcion, string motivo)
    {
        if (!PuedeAnular(recepcion))
            throw new InvalidOperationException("Solo se puede anular una recepción registrada.");

        if (!_sesion.Puede(Permisos.RecepcionesMateriaPrima.Anular))
            throw new InvalidOperationException("No tienes permiso para anular recepciones de materia prima.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var existencias = _materiaPrima.ExistenciasPorTipo();

        foreach (var linea in recepcion.Lineas)
        {
            var quedaria = existencias.GetValueOrDefault(linea.TipoMateriaPrimaId) - linea.Cantidad;

            if (quedaria < 0)
                throw new InvalidOperationException(
                    $"No se puede anular: de {linea.TipoMateriaPrimaNombre} ya se despachó lo que " +
                    "trajo esta recepción.");
        }

        var copia = recepcion.Clonar();
        copia.Estado = EstadoRecepcionMateriaPrima.Anulada;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _recepciones.Update(copia);
        return copia;
    }

    // --- Piezas internas ---

    /// <summary>
    /// Correlativo interno. Se calcula al registrar, no en el formulario, y el índice único
    /// <c>(OrganizacionId, Numero)</c> es la red por si dos puestos coincidieran.
    /// </summary>
    private string SiguienteNumero()
    {
        var ultimo = _recepciones.GetAll()
            .Select(r => r.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    // --- Resúmenes para el panel del módulo ---

    public IReadOnlyList<RecepcionMateriaPrima> DelMes()
    {
        var desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        return [.. _recepciones.GetAll()
            .Where(r => r.Estado == EstadoRecepcionMateriaPrima.Registrada && r.Fecha.Date >= desde)];
    }
}
