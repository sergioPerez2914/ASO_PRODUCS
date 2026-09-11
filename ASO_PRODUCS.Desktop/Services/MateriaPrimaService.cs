using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas del catálogo de tipos de materia prima y de su existencia.
///
/// Mismo criterio que <see cref="InventarioService"/>: la existencia no se guarda, se calcula.
/// Lo que entró (<see cref="RecepcionMateriaPrima"/>) menos lo que salió
/// (<see cref="SalidaMateriaPrima"/>), sin contar documentos anulados.
/// </summary>
public sealed class MateriaPrimaService
{
    private readonly ITipoMateriaPrimaDataSource _tipos;
    private readonly IRecepcionMateriaPrimaDataSource _recepciones;
    private readonly ISalidaMateriaPrimaDataSource _salidas;

    public MateriaPrimaService(ITipoMateriaPrimaDataSource tipos,
                               IRecepcionMateriaPrimaDataSource recepciones,
                               ISalidaMateriaPrimaDataSource salidas)
    {
        _tipos = tipos;
        _recepciones = recepciones;
        _salidas = salidas;
    }

    // --- Validación del maestro ---

    public bool Validar(TipoMateriaPrima tipo, out string? error)
    {
        if (string.IsNullOrWhiteSpace(tipo.Nombre))
        {
            error = "Indique el nombre del tipo de materia prima.";
            return false;
        }

        var repetido = _tipos.GetAll()
            .Where(t => t.Id != tipo.Id)
            .Any(t => string.Equals(t.Nombre.Trim(), tipo.Nombre.Trim(), System.StringComparison.OrdinalIgnoreCase));

        if (repetido)
        {
            error = $"Ya hay un tipo de materia prima llamado {tipo.Nombre.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Un tipo que ya se movió no se borra: se desactiva. Borrarlo dejaría las líneas de los
    /// documentos apuntando a un tipo que no existe y la existencia sin poder cuadrarse.
    /// </summary>
    public bool PuedeEliminar(TipoMateriaPrima tipo)
        => !_recepciones.GetAll().Any(r => r.Lineas.Any(l => l.TipoMateriaPrimaId == tipo.Id))
           && !_salidas.GetAll().Any(s => s.Lineas.Any(l => l.TipoMateriaPrimaId == tipo.Id));

    /// <summary>
    /// Da de alta, de una sola vez, los tipos sugeridos que todavía no existan por nombre
    /// (reutiliza <see cref="Validar"/>, que ya rechaza los repetidos). Pensado para precargar el
    /// catálogo de una planta nueva; correrlo más de una vez no duplica nada. Devuelve cuántos se
    /// crearon.
    /// </summary>
    public int CargarSugeridos(IEnumerable<(string Nombre, string UnidadMedida)> sugeridos)
    {
        var creados = 0;

        foreach (var (nombre, unidadMedida) in sugeridos)
        {
            var candidato = new TipoMateriaPrima { Nombre = nombre, UnidadMedida = unidadMedida, Activo = true };

            if (!Validar(candidato, out _))
                continue;

            _tipos.Add(candidato);
            creados++;
        }

        return creados;
    }

    // --- Existencia ---

    /// <summary>
    /// Existencia de cada tipo que se haya movido alguna vez: lo que entró menos lo que salió, sin
    /// contar documentos anulados.
    ///
    /// Recorre las dos tablas UNA vez cada una y suma en memoria sobre un diccionario, igual que
    /// <see cref="InventarioService.ExistenciasPorArticulo"/>.
    /// </summary>
    public IReadOnlyDictionary<int, decimal> ExistenciasPorTipo()
    {
        var saldos = new Dictionary<int, decimal>();

        foreach (var recepcion in _recepciones.GetAll().Where(r => r.CuentaEnExistencia))
            foreach (var linea in recepcion.Lineas)
                saldos[linea.TipoMateriaPrimaId] = saldos.GetValueOrDefault(linea.TipoMateriaPrimaId) + linea.Cantidad;

        foreach (var salida in _salidas.GetAll().Where(s => s.CuentaEnExistencia))
            foreach (var linea in salida.Lineas)
                saldos[linea.TipoMateriaPrimaId] = saldos.GetValueOrDefault(linea.TipoMateriaPrimaId) - linea.Cantidad;

        return saldos;
    }

    public decimal Existencia(int tipoId) => ExistenciasPorTipo().GetValueOrDefault(tipoId);

    /// <summary>
    /// Rellena <see cref="TipoMateriaPrima.Existencia"/> sobre los tipos que ya están en pantalla.
    ///
    /// Los modelos no avisan de sus cambios, así que quien llame a esto tiene que refrescar la
    /// vista después (<c>ItemsView.Refresh()</c>) o la grilla seguirá mostrando lo anterior.
    /// </summary>
    public void RellenarExistencias(IEnumerable<TipoMateriaPrima> tipos)
    {
        var saldos = ExistenciasPorTipo();

        foreach (var tipo in tipos)
            tipo.Existencia = saldos.GetValueOrDefault(tipo.Id);
    }

    /// <summary>Tipos activos con existencia, para elegir en una recepción o una salida.</summary>
    public IReadOnlyList<TipoMateriaPrima> ActivosConExistencia()
    {
        var tipos = _tipos.GetActivos().ToList();
        RellenarExistencias(tipos);
        return tipos;
    }

    // --- Resúmenes para el panel del módulo ---

    public int TotalTiposActivos() => _tipos.GetAll().Count(t => t.Activo);

    public int TiposSinExistencia()
    {
        var tipos = _tipos.GetAll().ToList();
        RellenarExistencias(tipos);
        return tipos.Count(t => t.SinExistencia);
    }
}
