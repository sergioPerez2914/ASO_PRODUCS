using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Costo de materiales de los procesos de producción, derivado y nunca guardado.
///
/// Cada línea consumida (las salidas registradas enlazadas al proceso) se valora al PROMEDIO
/// PONDERADO de las compras con precio de ese material hasta la fecha del consumo. Las entradas a
/// precio cero (recepciones OtroOrigen, entradas de Ajuste) no cuentan: arrastrarían el promedio
/// hacia abajo sin ser un costo real. Un material sin ninguna compra con precio queda "sin costo" y
/// marca el proceso como incompleto.
///
/// Un lote de producto consumido (transformación) se valora al costo por unidad del proceso que lo
/// produjo, calculado con estas mismas reglas; si ese costo era incompleto, el derivado también.
/// </summary>
public sealed class CostosProduccionService
{
    private readonly IRecepcionMateriaPrimaDataSource _recepciones;
    private readonly IEntradaInventarioDataSource _entradas;
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
        _recepciones = recepciones;
        _entradas = entradas;
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

        var compras = ComprasConPrecio();
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
                    CostoUnitario = Promedio(compras, (OrigenMaterial.MateriaPrima, l.TipoMateriaPrimaId), salida.Fecha)
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
                    CostoUnitario = Promedio(compras, (OrigenMaterial.Articulo, l.ArticuloId), salida.Fecha)
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

    private Dictionary<(OrigenMaterial, int), List<(DateTime Fecha, decimal Cantidad, decimal Subtotal)>> ComprasConPrecio()
    {
        var compras = new Dictionary<(OrigenMaterial, int), List<(DateTime, decimal, decimal)>>();

        void Agregar((OrigenMaterial, int) clave, DateTime fecha, decimal cantidad, decimal precio)
        {
            if (precio <= 0 || cantidad <= 0)
                return;
            if (!compras.TryGetValue(clave, out var lista))
                compras[clave] = lista = [];
            lista.Add((fecha.Date, cantidad, cantidad * precio));
        }

        foreach (var recepcion in _recepciones.GetAll().Where(r => r.CuentaEnExistencia))
            foreach (var l in recepcion.Lineas)
                Agregar((OrigenMaterial.MateriaPrima, l.TipoMateriaPrimaId), recepcion.Fecha, l.Cantidad, l.PrecioUnitario);

        foreach (var entrada in _entradas.GetAll().Where(e => e.CuentaEnKardex))
            foreach (var l in entrada.Lineas)
                Agregar((OrigenMaterial.Articulo, l.ArticuloId), entrada.Fecha, l.Cantidad, l.PrecioUnitario);

        return compras;
    }

    private static decimal? Promedio(
        Dictionary<(OrigenMaterial, int), List<(DateTime Fecha, decimal Cantidad, decimal Subtotal)>> compras,
        (OrigenMaterial, int) clave, DateTime fecha)
    {
        if (!compras.TryGetValue(clave, out var lista))
            return null;

        var previas = lista.Where(c => c.Fecha <= fecha.Date).ToList();
        var cantidad = previas.Sum(c => c.Cantidad);

        return cantidad > 0 ? previas.Sum(c => c.Subtotal) / cantidad : null;
    }
}
