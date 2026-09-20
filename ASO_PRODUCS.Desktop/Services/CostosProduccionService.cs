using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Costo de materiales de los procesos de producción, derivado y nunca guardado.
///
/// Cada línea consumida (las salidas registradas enlazadas al proceso) se valora al PROMEDIO
/// PONDERADO de las compras con precio de ese material hasta la fecha del consumo, que calcula
/// <see cref="CostosMaterialesService"/> — la misma regla, y el mismo número, que muestra la ficha
/// de un artículo. Un material sin ninguna compra con precio queda "sin costo" y marca el proceso
/// como incompleto.
///
/// Un lote de producto consumido (transformación) se valora al costo por unidad del proceso que lo
/// produjo, calculado con estas mismas reglas; si ese costo era incompleto, el derivado también.
/// </summary>
public sealed class CostosProduccionService
{
    private readonly CostosMaterialesService _materiales;
    private readonly ISalidaMateriaPrimaDataSource _salidasMateriaPrima;
    private readonly ISalidaInventarioDataSource _salidasInventario;
    private readonly IProductoDataSource _productos;
    private readonly IProcesoProduccionDataSource _procesos;

    public CostosProduccionService(IRecepcionMateriaPrimaDataSource recepciones,
                                   IEntradaInventarioDataSource entradas,
                                   ISalidaMateriaPrimaDataSource salidasMateriaPrima,
                                   ISalidaInventarioDataSource salidasInventario,
                                   IProductoDataSource productos,
                                   IProcesoProduccionDataSource procesos)
    {
        _materiales = new CostosMaterialesService(recepciones, entradas, salidasMateriaPrima, salidasInventario);
        _salidasMateriaPrima = salidasMateriaPrima;
        _salidasInventario = salidasInventario;
        _productos = productos;
        _procesos = procesos;
    }

    public CostoProceso Calcular(ProcesoProduccion proceso) => Calcular([proceso])[proceso.Id];

    /// <summary>Lee cada tabla una sola vez, sin importar cuántos procesos se pidan.</summary>
    public IReadOnlyDictionary<int, CostoProceso> Calcular(IEnumerable<ProcesoProduccion> procesos)
    {
        var lista = procesos.ToList();
        var todos = _procesos.GetAll().ToDictionary(p => p.Id);

        foreach (var p in lista)
            todos[p.Id] = p;

        var compras = _materiales.ComprasConPrecio();
        var precios = _productos.GetAll().ToDictionary(p => p.Id, p => p.PrecioUnitario);

        var lineasPorProceso = new Dictionary<int, List<LineaCostoProceso>>();

        void Agregar(int procesoId, LineaCostoProceso linea)
        {
            if (!lineasPorProceso.TryGetValue(procesoId, out var lineas))
                lineasPorProceso[procesoId] = lineas = [];
            lineas.Add(linea);
        }

        foreach (var salida in _salidasMateriaPrima.GetAll()
                     .Where(s => s.CuentaEnExistencia && s.ProcesoProduccionId is not null))
            foreach (var l in salida.Lineas)
                Agregar(salida.ProcesoProduccionId!.Value, new LineaCostoProceso
                {
                    Origen = OrigenMaterial.MateriaPrima,
                    MaterialNombre = l.TipoMateriaPrimaNombre,
                    Unidad = l.UnidadMedidaSnapshot,
                    Cantidad = l.Cantidad,
                    EsMerma = salida.Motivo == MotivoSalidaMateriaPrima.Merma,
                    CostoUnitario = CostosMaterialesService.Promedio(compras, (OrigenMaterial.MateriaPrima, l.TipoMateriaPrimaId), salida.Fecha)
                });

        foreach (var salida in _salidasInventario.GetAll()
                     .Where(s => s.CuentaEnKardex && s.ProcesoProduccionId is not null))
            foreach (var l in salida.Lineas)
                Agregar(salida.ProcesoProduccionId!.Value, new LineaCostoProceso
                {
                    Origen = OrigenMaterial.Articulo,
                    MaterialNombre = l.ArticuloNombre,
                    Unidad = l.UnidadTexto,
                    Cantidad = l.Cantidad,
                    EsMerma = salida.Motivo == MotivoSalida.Merma,
                    CostoUnitario = CostosMaterialesService.Promedio(compras, (OrigenMaterial.Articulo, l.ArticuloId), salida.Fecha)
                });

        var memo = new Dictionary<int, CostoProceso>();
        var enCurso = new HashSet<int>();

        CostoProceso Construir(ProcesoProduccion p)
        {
            if (memo.TryGetValue(p.Id, out var hecho))
                return hecho;

            enCurso.Add(p.Id);

            var lineas = lineasPorProceso.TryGetValue(p.Id, out var deSalidas) ? new List<LineaCostoProceso>(deSalidas) : [];

            var deProducto = p.LineasIniciales
                .Where(l => l.Origen == OrigenMaterial.Producto)
                .Select(l => (l.MaterialNombre, l.UnidadMedidaSnapshot, l.Cantidad, l.LoteProcesoId, l.LoteNumero, EsMerma: false))
                .Concat(p.Etapas.SelectMany(e => e.Lineas)
                    .Where(l => l.Origen == OrigenMaterial.Producto)
                    .Select(l => (l.MaterialNombre, l.UnidadMedidaSnapshot, l.Cantidad, l.LoteProcesoId, l.LoteNumero,
                                  EsMerma: l.Motivo == MotivoConsumoEtapa.Merma)));

            foreach (var l in deProducto)
            {
                // El lote origen siempre es anterior; enCurso solo protege de un dato corrupto.
                var origen = l.LoteProcesoId is { } loteId && todos.TryGetValue(loteId, out var proceso)
                             && !enCurso.Contains(loteId)
                    ? Construir(proceso)
                    : null;

                lineas.Add(new LineaCostoProceso
                {
                    Origen = OrigenMaterial.Producto,
                    MaterialNombre = l.MaterialNombre,
                    Unidad = l.UnidadMedidaSnapshot,
                    Cantidad = l.Cantidad,
                    EsMerma = l.EsMerma,
                    LoteNumero = l.LoteNumero,
                    CostoUnitario = origen?.CostoPorUnidad,
                    CostoParcial = origen?.Incompleto ?? false
                });
            }

            enCurso.Remove(p.Id);

            return memo[p.Id] = new CostoProceso
            {
                ProcesoId = p.Id,
                Lineas = lineas,
                CantidadProducida = p.Estado == EstadoProcesoProduccion.Terminado ? p.CantidadProducida : null,
                UnidadMedida = p.UnidadMedidaSnapshot,
                PrecioVenta = precios.GetValueOrDefault(p.ProductoId)
            };
        }

        return lista.ToDictionary(p => p.Id, Construir);
    }
}
