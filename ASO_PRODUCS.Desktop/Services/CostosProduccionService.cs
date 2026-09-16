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
/// </summary>
public sealed class CostosProduccionService
{
    private readonly IRecepcionMateriaPrimaDataSource _recepciones;
    private readonly IEntradaInventarioDataSource _entradas;
    private readonly ISalidaMateriaPrimaDataSource _salidasMateriaPrima;
    private readonly ISalidaInventarioDataSource _salidasInventario;
    private readonly IProductoDataSource _productos;

    public CostosProduccionService(IRecepcionMateriaPrimaDataSource recepciones,
                                   IEntradaInventarioDataSource entradas,
                                   ISalidaMateriaPrimaDataSource salidasMateriaPrima,
                                   ISalidaInventarioDataSource salidasInventario,
                                   IProductoDataSource productos)
    {
        _recepciones = recepciones;
        _entradas = entradas;
        _salidasMateriaPrima = salidasMateriaPrima;
        _salidasInventario = salidasInventario;
        _productos = productos;
    }

    public CostoProceso Calcular(ProcesoProduccion proceso) => Calcular([proceso])[proceso.Id];

    /// <summary>Lee cada tabla una sola vez, sin importar cuántos procesos se pidan.</summary>
    public IReadOnlyDictionary<int, CostoProceso> Calcular(IEnumerable<ProcesoProduccion> procesos)
    {
        var lista = procesos.ToList();
        var ids = lista.Select(p => p.Id).ToHashSet();
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
                     .Where(s => s.CuentaEnExistencia && s.ProcesoProduccionId is { } id && ids.Contains(id)))
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
                     .Where(s => s.CuentaEnKardex && s.ProcesoProduccionId is { } id && ids.Contains(id)))
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

        return lista.ToDictionary(p => p.Id, p => new CostoProceso
        {
            ProcesoId = p.Id,
            Lineas = lineasPorProceso.TryGetValue(p.Id, out var lineas) ? lineas : [],
            CantidadProducida = p.Estado == EstadoProcesoProduccion.Terminado ? p.CantidadProducida : null,
            UnidadMedida = p.UnidadMedidaSnapshot,
            PrecioVenta = precios.GetValueOrDefault(p.ProductoId)
        });
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
