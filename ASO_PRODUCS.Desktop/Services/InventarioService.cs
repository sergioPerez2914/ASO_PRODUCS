using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas del catálogo de artículos y del kardex.
///
/// El almacén no guarda existencias: las calcula. Este servicio es el único sitio donde vive esa
/// cuenta, y lo usan las tres pantallas del módulo — Almacén para pintarla, Salidas para no
/// dejar sacar más de lo que hay, y Entradas para no dejar anular lo que ya se despachó.
/// </summary>
public sealed class InventarioService
{
    private const string Prefijo = "ART-";

    private readonly IArticuloDataSource _articulos;
    private readonly IEntradaInventarioDataSource _entradas;
    private readonly ISalidaInventarioDataSource _salidas;

    public InventarioService(IArticuloDataSource articulos,
                             IEntradaInventarioDataSource entradas,
                             ISalidaInventarioDataSource salidas)
    {
        _articulos = articulos;
        _entradas = entradas;
        _salidas = salidas;
    }

    // --- Código ---

    /// <summary>Código aleatorio y legible, tipo "ART-K7P2Q", para cuando quien da de alta el
    /// artículo no escribe uno propio. Ver <see cref="Codigos.Generar"/>.</summary>
    public string GenerarCodigo() =>
        Codigos.Generar(Prefijo, _articulos.GetAll().Select(a => a.Codigo), "el artículo");

    // --- Validación del maestro ---

    public bool Validar(Articulo articulo, out string? error)
    {
        if (string.IsNullOrWhiteSpace(articulo.Nombre))
        {
            error = "Indique el nombre del artículo.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(articulo.Codigo))
        {
            error = "Indique el código del artículo o deje el campo vacío para generarlo.";
            return false;
        }

        if (articulo.Minimo < 0)
        {
            error = "El mínimo no puede ser negativo.";
            return false;
        }

        var repetido = _articulos.GetAll()
            .Where(a => a.Id != articulo.Id)
            .Any(a => string.Equals(a.Codigo.Trim(), articulo.Codigo.Trim(),
                                    StringComparison.OrdinalIgnoreCase));

        if (repetido)
        {
            error = $"Ya hay un artículo con el código {articulo.Codigo.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Da de alta, de una sola vez, los artículos sugeridos que todavía no existan por nombre.
    /// Pensado para precargar el almacén de una instalación nueva; correrlo más de una vez no
    /// duplica nada. Devuelve cuántos se crearon.
    ///
    /// Compara por NOMBRE y no por código, a diferencia de <see cref="Validar"/>: el código lo
    /// genera esta misma llamada, así que dos ejecuciones seguidas producirían dos códigos
    /// distintos para el mismo artículo y ninguna se vería como repetida.
    ///
    /// Es el gemelo de <c>MateriaPrimaService.CargarSugeridos</c> y de
    /// <c>ProductosService.CargarSugeridos</c>; Almacén era el único de los tres catálogos que
    /// no tenía con qué arrancar.
    /// </summary>
    public int CargarSugeridos(IEnumerable<(string Nombre, string UnidadMedida)> sugeridos)
    {
        var existentes = _articulos.GetAll()
            .Select(a => a.Nombre.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var creados = 0;

        foreach (var (nombre, unidadMedida) in sugeridos)
        {
            if (!existentes.Add(nombre.Trim()))
                continue;

            _articulos.Add(new Articulo
            {
                Codigo = GenerarCodigo(),
                Nombre = nombre,
                UnidadMedida = unidadMedida,
                Activo = true
            });

            creados++;
        }

        return creados;
    }

    /// <summary>
    /// Un artículo que ya se movió no se borra: se desactiva. Borrarlo dejaría las líneas de los
    /// documentos apuntando a un artículo que no existe y el kardex sin poder cuadrarse.
    /// </summary>
    public bool PuedeEliminar(Articulo articulo)
        => !_entradas.GetAll().Any(e => e.Lineas.Any(l => l.ArticuloId == articulo.Id))
           && !_salidas.GetAll().Any(s => s.Lineas.Any(l => l.ArticuloId == articulo.Id));

    // --- Kardex ---

    /// <summary>
    /// Existencia de cada artículo que se haya movido alguna vez: lo que entró menos lo que
    /// salió, sin contar documentos anulados.
    ///
    /// Recorre las dos tablas UNA vez cada una y suma en memoria sobre un diccionario. No
    /// consulta por artículo: hacerlo sería una consulta por fila de la grilla del almacén.
    /// </summary>
    public IReadOnlyDictionary<int, decimal> ExistenciasPorArticulo()
    {
        var saldos = new Dictionary<int, decimal>();

        foreach (var entrada in _entradas.GetAll().Where(e => e.CuentaEnKardex))
            foreach (var linea in entrada.Lineas)
                saldos[linea.ArticuloId] = saldos.GetValueOrDefault(linea.ArticuloId) + linea.Cantidad;

        foreach (var salida in _salidas.GetAll().Where(s => s.CuentaEnKardex))
            foreach (var linea in salida.Lineas)
                saldos[linea.ArticuloId] = saldos.GetValueOrDefault(linea.ArticuloId) - linea.Cantidad;

        return saldos;
    }

    public decimal Existencia(int articuloId) => ExistenciasPorArticulo().GetValueOrDefault(articuloId);

    /// <summary>
    /// Rellena <see cref="Articulo.Existencia"/> sobre los artículos que ya están en pantalla.
    ///
    /// Los modelos no avisan de sus cambios, así que quien llame a esto tiene que refrescar la
    /// vista después (<c>ItemsView.Refresh()</c>) o la grilla seguirá mostrando lo anterior.
    /// </summary>
    public void RellenarExistencias(IEnumerable<Articulo> articulos)
    {
        var saldos = ExistenciasPorArticulo();

        foreach (var articulo in articulos)
            articulo.Existencia = saldos.GetValueOrDefault(articulo.Id);
    }

    /// <summary>Artículos activos con existencia, para elegir en una entrada o una salida.</summary>
    public IReadOnlyList<Articulo> ActivosConExistencia()
    {
        var articulos = _articulos.GetActivos().ToList();
        RellenarExistencias(articulos);
        return articulos;
    }

    /// <summary>Artículos activos, sin filtrar por existencia: para un combo de línea de consumo
    /// manual (Transformar, Iniciar proceso), donde la validación de existencia es cosa del
    /// servicio al confirmar, no del combo.</summary>
    public IReadOnlyList<Articulo> ArticulosActivos() => [.. _articulos.GetActivos()];

    // --- Resúmenes para el panel del módulo ---

    public int TotalArticulosActivos() => _articulos.GetAll().Count(a => a.Activo);

    public int ArticulosBajoMinimo()
    {
        var articulos = _articulos.GetAll().ToList();
        RellenarExistencias(articulos);
        return articulos.Count(a => a.BajoMinimo);
    }

    public int ArticulosSinExistencia()
    {
        var articulos = _articulos.GetAll().ToList();
        RellenarExistencias(articulos);
        return articulos.Count(a => a.SinExistencia);
    }
}
