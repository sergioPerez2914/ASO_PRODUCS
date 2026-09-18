using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas del catálogo de productos terminados y de su existencia.
///
/// Mismo criterio que <see cref="MateriaPrimaService"/>/<see cref="InventarioService"/>: la
/// existencia no se guarda, se calcula. Lo que produjeron los procesos Terminados menos lo que
/// salió en un despacho, sin contar documentos anulados.
/// </summary>
public sealed class ProductosService
{
    private readonly IProductoDataSource _productos;
    private readonly IProcesoProduccionDataSource _procesos;
    private readonly IDespachoDataSource _despachos;

    public ProductosService(IProductoDataSource productos,
                            IProcesoProduccionDataSource procesos,
                            IDespachoDataSource despachos)
    {
        _productos = productos;
        _procesos = procesos;
        _despachos = despachos;
    }

    // --- Validación del maestro ---

    public bool Validar(Producto producto, out string? error)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre))
        {
            error = "Indique el nombre del producto.";
            return false;
        }

        var repetido = _productos.GetAll()
            .Where(p => p.Id != producto.Id)
            .Any(p => string.Equals(p.Nombre.Trim(), producto.Nombre.Trim(), System.StringComparison.OrdinalIgnoreCase));

        if (repetido)
        {
            error = $"Ya hay un producto llamado {producto.Nombre.Trim()}.";
            return false;
        }

        if (producto.PrecioUnitario < 0)
        {
            error = "El precio no puede ser negativo.";
            return false;
        }

        if (producto.DiasVidaUtil is <= 0)
        {
            error = "Los días de vida útil deben ser mayores que cero, o déjelos vacíos si no vence.";
            return false;
        }

        if (producto.Minimo < 0)
        {
            error = "El mínimo no puede ser negativo.";
            return false;
        }

        return ValidarPresentacion(producto, out error);
    }

    private bool ValidarPresentacion(Producto producto, out string? error)
    {
        if (producto.ProductoBaseId is not { } baseId)
        {
            error = null;
            return true;
        }

        if (baseId == producto.Id)
        {
            error = "Un producto no puede ser presentación de sí mismo.";
            return false;
        }

        if (producto.CantidadBasePorUnidad is not > 0)
        {
            error = $"Indique cuánto de {producto.ProductoBaseNombre} lleva cada unidad, con un número mayor que cero.";
            return false;
        }

        // Una cadena Mantequilla → Ghee → Mantequilla haría que ninguno de los dos se pudiera
        // producir sin el otro.
        if (producto.Id != 0)
        {
            var porId = _productos.GetAll().ToDictionary(p => p.Id);
            var visitados = new HashSet<int> { producto.Id };

            for (int? actual = baseId; actual is { } id && porId.TryGetValue(id, out var p); actual = p.ProductoBaseId)
            {
                if (!visitados.Add(id))
                {
                    error = $"{producto.ProductoBaseNombre} ya sale, directa o indirectamente, de {producto.Nombre}.";
                    return false;
                }
            }
        }

        if (producto.Componentes.Any(c => c.MaterialId == 0))
        {
            error = "Hay un material de empaque sin seleccionar.";
            return false;
        }

        if (producto.Componentes.Any(c => c.CantidadPorUnidad <= 0))
        {
            error = "La cantidad por unidad de cada material debe ser mayor que cero.";
            return false;
        }

        if (producto.Componentes.GroupBy(c => (c.Origen, c.MaterialId)).FirstOrDefault(g => g.Count() > 1) is { } repetido)
        {
            error = $"{repetido.First().MaterialNombre} está más de una vez; júntelo en una sola línea.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Un producto que ya se produjo o se despachó no se borra: se desactiva. Borrarlo dejaría
    /// esos documentos apuntando a un producto que no existe y la existencia sin poder cuadrarse.
    /// </summary>
    public bool PuedeEliminar(Producto producto)
        => !_procesos.GetAll().Any(p => p.ProductoId == producto.Id)
           && !_despachos.GetAll().Any(d => d.Lineas.Any(l => l.ProductoId == producto.Id))
           && !_productos.GetAll().Any(p => p.ProductoBaseId == producto.Id);

    /// <summary>
    /// Da de alta, de una sola vez, los productos sugeridos que todavía no existan por nombre
    /// (reutiliza <see cref="Validar"/>, que ya rechaza los repetidos). Pensado para precargar el
    /// catálogo de una planta nueva; correrlo más de una vez no duplica nada. Devuelve cuántos se
    /// crearon.
    /// </summary>
    public int CargarSugeridos(IEnumerable<(string Nombre, string UnidadMedida)> sugeridos)
    {
        var creados = 0;

        foreach (var (nombre, unidadMedida) in sugeridos)
        {
            var candidato = new Producto { Nombre = nombre, UnidadMedida = unidadMedida, Activo = true };

            if (!Validar(candidato, out _))
                continue;

            _productos.Add(candidato);
            creados++;
        }

        return creados;
    }

    // --- Existencia ---

    /// <summary>
    /// Existencia de cada producto que se haya movido alguna vez: lo producido por los procesos
    /// Terminados, menos lo despachado, menos lo que consumieron otros procesos (transformaciones),
    /// sin contar documentos anulados.
    ///
    /// A diferencia de la materia prima y los artículos, el producto que consumió un proceso
    /// anulado SÍ vuelve: no hay una entrada de ajuste de productos con la que devolverlo.
    ///
    /// Recorre las dos tablas UNA vez cada una y suma en memoria sobre un diccionario, igual que
    /// <see cref="MateriaPrimaService.ExistenciasPorTipo"/>.
    /// </summary>
    public IReadOnlyDictionary<int, decimal> ExistenciasPorProducto()
    {
        var saldos = new Dictionary<int, decimal>();

        foreach (var proceso in _procesos.GetAll())
        {
            if (proceso.CuentaEnExistencia)
                saldos[proceso.ProductoId] = saldos.GetValueOrDefault(proceso.ProductoId) + (proceso.CantidadProducida ?? 0);

            if (proceso.Estado != EstadoProcesoProduccion.Anulado)
                foreach (var (productoId, _, cantidad) in proceso.ConsumosDeProducto())
                    saldos[productoId] = saldos.GetValueOrDefault(productoId) - cantidad;
        }

        foreach (var despacho in _despachos.GetAll().Where(d => d.CuentaEnExistencia))
            foreach (var linea in despacho.Lineas)
                saldos[linea.ProductoId] = saldos.GetValueOrDefault(linea.ProductoId) - linea.Cantidad;

        return saldos;
    }

    public decimal Existencia(int productoId) => ExistenciasPorProducto().GetValueOrDefault(productoId);

    public Producto? Buscar(int productoId) => _productos.GetById(productoId);

    /// <summary>Los productos activos que tienen receta de presentación, para transformar.</summary>
    public IReadOnlyList<Producto> DerivadosActivos() => [.. _productos.GetActivos().Where(p => p.EsDerivado)];

    /// <summary>
    /// Cada proceso Terminado es un lote: lo producido menos las líneas de despacho que lo citan,
    /// en orden FEFO (vence antes primero; los que no vencen al final, por fecha de término).
    ///
    /// Las líneas de despacho anteriores al manejo de lotes no traen lote: se descuentan en
    /// memoria de los lotes de su producto en ese mismo orden, sin tocar la base. Así la suma por
    /// lote sigue cuadrando con <see cref="ExistenciasPorProducto"/>.
    /// </summary>
    public IReadOnlyList<LoteProducto> Lotes()
    {
        var procesos = _procesos.GetAll().ToList();

        var lotes = procesos
            .Where(p => p.CuentaEnExistencia)
            .Select(p => new LoteProducto
            {
                ProcesoId = p.Id,
                Numero = p.Numero,
                ProductoId = p.ProductoId,
                ProductoNombre = p.ProductoNombre,
                Unidad = p.UnidadMedidaSnapshot,
                FechaTermino = p.FechaTermino,
                FechaVencimiento = p.FechaVencimiento,
                Producido = p.CantidadProducida ?? 0
            })
            .OrderBy(l => l.FechaVencimiento is null)
            .ThenBy(l => l.FechaVencimiento)
            .ThenBy(l => l.FechaTermino)
            .ToList();

        var porId = lotes.ToDictionary(l => l.ProcesoId);
        var sinLote = new Dictionary<int, decimal>();

        // Toda línea de consumo de producto nace con lote (el servicio lo exige), así que no hay
        // un "sin lote" que repartir como con los despachos viejos.
        foreach (var proceso in procesos.Where(p => p.Estado != EstadoProcesoProduccion.Anulado))
            foreach (var (_, loteId, cantidad) in proceso.ConsumosDeProducto())
                if (loteId is { } id && porId.TryGetValue(id, out var loteConsumido))
                    loteConsumido.Transformado += cantidad;

        foreach (var despacho in _despachos.GetAll().Where(d => d.CuentaEnExistencia))
            foreach (var linea in despacho.Lineas)
            {
                if (linea.ProcesoProduccionId is { } id && porId.TryGetValue(id, out var lote))
                    lote.Despachado += linea.Cantidad;
                else
                    sinLote[linea.ProductoId] = sinLote.GetValueOrDefault(linea.ProductoId) + linea.Cantidad;
            }

        // Los despachos sin lote son anteriores a esta funcionalidad: hay que imputarlos a los
        // lotes más viejos primero (orden cronológico de producción), NO al orden FEFO de
        // arriba (vence antes primero) — ese orden pondría un lote recién terminado con
        // vencimiento cargado por delante de lotes sin vencimiento pero más antiguos, atribuyéndole
        // despachos de fechas anteriores a que ese lote existiera.
        var ordenCronologico = lotes.OrderBy(l => l.FechaTermino).ToList();

        foreach (var (productoId, cantidad) in sinLote)
        {
            var pendiente = cantidad;

            foreach (var lote in ordenCronologico.Where(l => l.ProductoId == productoId && l.Existencia > 0))
            {
                if (pendiente <= 0)
                    break;

                var tomado = Math.Min(pendiente, lote.Existencia);
                lote.Despachado += tomado;
                pendiente -= tomado;
            }
        }

        return lotes;
    }

    public IReadOnlyList<LoteProducto> LotesConExistencia() => [.. Lotes().Where(l => l.Existencia > 0)];

    /// <summary>
    /// Rellena <see cref="Producto.Existencia"/> sobre los productos que ya están en pantalla.
    ///
    /// Los modelos no avisan de sus cambios, así que quien llame a esto tiene que refrescar la
    /// vista después (<c>ItemsView.Refresh()</c>) o la grilla seguirá mostrando lo anterior.
    /// </summary>
    public void RellenarExistencias(IEnumerable<Producto> productos)
    {
        var saldos = ExistenciasPorProducto();

        foreach (var producto in productos)
            producto.Existencia = saldos.GetValueOrDefault(producto.Id);
    }

    /// <summary>
    /// Rellena el resumen de lotes de cada producto —cuántos tienen existencia y cuál vence
    /// primero—, que es lo que pinta la tarjeta del catálogo.
    ///
    /// <paramref name="lotes"/> existe para no pagar dos veces el mismo cálculo: <see cref="Lotes"/>
    /// recorre procesos y despachos enteros, y la pantalla de Productos y Lotes ya los tiene
    /// calculados para su otra pestaña. Quien no los tenga, lo omite y se calculan aquí.
    ///
    /// Mismo contrato que <see cref="RellenarExistencias"/>: los modelos no avisan de sus cambios,
    /// así que hay que refrescar la vista después.
    /// </summary>
    public void RellenarResumenDeLotes(IEnumerable<Producto> productos,
                                       IReadOnlyList<LoteProducto>? lotes = null)
    {
        // Solo los que tienen existencia: un lote agotado no cuenta como lote del producto, igual
        // que no suma a la existencia.
        var porProducto = (lotes ?? Lotes())
            .Where(l => l.Existencia > 0)
            .GroupBy(l => l.ProductoId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var producto in productos)
        {
            if (!porProducto.TryGetValue(producto.Id, out var suyos))
            {
                producto.LotesConExistencia = 0;
                producto.ProximoVencimiento = null;
                continue;
            }

            producto.LotesConExistencia = suyos.Count;

            // El más próximo a vencer. Los que no vencen no compiten por este puesto: un lote sin
            // fecha no es "el que hay que sacar primero" para quien mira la tarjeta.
            producto.ProximoVencimiento = suyos
                .Where(l => l.FechaVencimiento is not null)
                .Min(l => l.FechaVencimiento);
        }
    }

    /// <summary>Productos activos con existencia, para elegir al iniciar un proceso o registrar
    /// un despacho.</summary>
    public IReadOnlyList<Producto> ActivosConExistencia()
    {
        var productos = _productos.GetActivos().ToList();
        RellenarExistencias(productos);
        return productos;
    }

    // --- Resúmenes para el panel del módulo ---

    public int TotalProductosActivos() => _productos.GetAll().Count(p => p.Activo);

    public int ProductosSinExistencia()
    {
        var productos = _productos.GetAll().ToList();
        RellenarExistencias(productos);
        return productos.Count(p => p.SinExistencia);
    }

    public int ProductosBajoMinimo()
    {
        var productos = _productos.GetAll().ToList();
        RellenarExistencias(productos);
        return productos.Count(p => p.BajoMinimo);
    }
}
