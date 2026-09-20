using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>Una compra con precio de un material, reducida a lo que hace falta para promediarla.</summary>
public readonly record struct CompraConPrecio(DateTime Fecha, decimal Cantidad, decimal Subtotal);

/// <summary>
/// Cuánto cuesta un material del almacén o de materia prima, y de qué compras sale ese número.
///
/// Es el ÚNICO sitio donde se define el precio promedio: un material se valora al PROMEDIO
/// PONDERADO de las compras con precio hasta una fecha. Las entradas a precio cero (recepciones
/// OtroOrigen, entradas de Ajuste) no cuentan: arrastrarían el promedio hacia abajo sin ser un
/// costo real. Vivía privado dentro de <see cref="CostosProduccionService"/>, que ahora llama
/// aquí — si esta regla cambia, tiene que cambiar para el costo de un proceso y para la ficha de
/// un artículo a la vez, o los dos números dirían cosas distintas de lo mismo.
///
/// Nada de esto se persiste: se recalcula cada vez, igual que la existencia.
/// </summary>
public sealed class CostosMaterialesService
{
    private readonly IRecepcionMateriaPrimaDataSource _recepciones;
    private readonly IEntradaInventarioDataSource _entradas;
    private readonly ISalidaMateriaPrimaDataSource _salidasMateriaPrima;
    private readonly ISalidaInventarioDataSource _salidasInventario;

    public CostosMaterialesService(IRecepcionMateriaPrimaDataSource recepciones,
                                   IEntradaInventarioDataSource entradas,
                                   ISalidaMateriaPrimaDataSource salidasMateriaPrima,
                                   ISalidaInventarioDataSource salidasInventario)
    {
        _recepciones = recepciones;
        _entradas = entradas;
        _salidasMateriaPrima = salidasMateriaPrima;
        _salidasInventario = salidasInventario;
    }

    /// <summary>
    /// Las compras con precio de cada material, en una sola pasada por cada tabla. Quien necesite
    /// promediar varias veces (el costo de un proceso valora cada línea a la fecha de su consumo)
    /// pide esto una vez y llama a <see cref="Promedio"/> tantas veces como haga falta.
    /// </summary>
    public Dictionary<(OrigenMaterial, int), List<CompraConPrecio>> ComprasConPrecio()
    {
        var compras = new Dictionary<(OrigenMaterial, int), List<CompraConPrecio>>();

        void Agregar((OrigenMaterial, int) clave, DateTime fecha, decimal cantidad, decimal precio)
        {
            if (precio <= 0 || cantidad <= 0)
                return;
            if (!compras.TryGetValue(clave, out var lista))
                compras[clave] = lista = [];
            lista.Add(new CompraConPrecio(fecha.Date, cantidad, cantidad * precio));
        }

        foreach (var recepcion in _recepciones.GetAll().Where(r => r.CuentaEnExistencia))
            foreach (var l in recepcion.Lineas)
                Agregar((OrigenMaterial.MateriaPrima, l.TipoMateriaPrimaId), recepcion.Fecha, l.Cantidad, l.PrecioUnitario);

        foreach (var entrada in _entradas.GetAll().Where(e => e.CuentaEnKardex))
            foreach (var l in entrada.Lineas)
                Agregar((OrigenMaterial.Articulo, l.ArticuloId), entrada.Fecha, l.Cantidad, l.PrecioUnitario);

        return compras;
    }

    /// <summary>Promedio ponderado de las compras anteriores o iguales a <paramref name="fecha"/>.
    /// Nulo si no hay ninguna: no es que valga cero, es que no se sabe.</summary>
    public static decimal? Promedio(Dictionary<(OrigenMaterial, int), List<CompraConPrecio>> compras,
                                    (OrigenMaterial, int) clave,
                                    DateTime fecha) =>
        compras.TryGetValue(clave, out var lista)
            ? PromedioDe(lista.Where(c => c.Fecha <= fecha.Date))
            : null;

    private static decimal? PromedioDe(IEnumerable<CompraConPrecio> compras)
    {
        var lista = compras.ToList();
        var cantidad = lista.Sum(c => c.Cantidad);

        return cantidad > 0 ? lista.Sum(c => c.Subtotal) / cantidad : null;
    }

    /// <summary>
    /// El promedio de cada material con todas sus compras, para rellenar la columna de las grillas
    /// de Almacén y Existencias. Solo trae los que tienen alguna compra con precio: el resto no
    /// está en el diccionario, que es lo que la grilla pinta como "—".
    ///
    /// No acota por fecha, a diferencia de <see cref="Promedio"/>: la grilla dice cuánto vale hoy
    /// lo que hay, y tiene que dar el mismo número que el indicador de la ficha, que promedia
    /// exactamente las compras que lista.
    /// </summary>
    public IReadOnlyDictionary<(OrigenMaterial, int), decimal> PromediosActuales()
    {
        var promedios = new Dictionary<(OrigenMaterial, int), decimal>();

        foreach (var (clave, compras) in ComprasConPrecio())
            if (PromedioDe(compras) is { } promedio)
                promedios[clave] = promedio;

        return promedios;
    }

    /// <summary>
    /// Rellena <see cref="Articulo.PrecioPromedio"/> sobre los artículos que ya están en pantalla.
    ///
    /// Va aquí y no en <c>InventarioService</c> (que es quien rellena la existencia) porque el
    /// promedio de un artículo y el de un tipo de materia prima salen de la misma regla y del mismo
    /// recorrido; partirlo en dos obligaría a darle a cada servicio de módulo las fuentes del otro.
    /// Los modelos no avisan de sus cambios, así que quien llame a esto tiene que refrescar la
    /// vista después (<c>ItemsView.Refresh()</c>), igual que con la existencia.
    /// </summary>
    public void RellenarPrecios(IEnumerable<Articulo> articulos)
    {
        var promedios = PromediosActuales();

        foreach (var articulo in articulos)
            articulo.PrecioPromedio = promedios.GetValueOrDefault((OrigenMaterial.Articulo, articulo.Id));
    }

    /// <summary>Lo mismo para <see cref="TipoMateriaPrima.PrecioPromedio"/>.</summary>
    public void RellenarPrecios(IEnumerable<TipoMateriaPrima> tipos)
    {
        var promedios = PromediosActuales();

        foreach (var tipo in tipos)
            tipo.PrecioPromedio = promedios.GetValueOrDefault((OrigenMaterial.MateriaPrima, tipo.Id));
    }

    /// <summary>La ficha de un artículo del almacén.</summary>
    public FichaMaterial Ficha(Articulo articulo) =>
        Ficha(OrigenMaterial.Articulo, articulo.Id, articulo.Nombre, articulo.UnidadMedida,
              articulo.Minimo, articulo.EstadoTexto, articulo.Codigo);

    /// <summary>La ficha de un tipo de materia prima.</summary>
    public FichaMaterial Ficha(TipoMateriaPrima tipo) =>
        Ficha(OrigenMaterial.MateriaPrima, tipo.Id, tipo.Nombre, tipo.UnidadMedida,
              tipo.Minimo, tipo.EstadoTexto, tipo.Codigo);

    /// <summary>
    /// La ficha de UN material: sus entradas con el reparto FIFO ya hecho, y sus salidas.
    ///
    /// Filtra en memoria sobre el <c>GetAll()</c> de cada tabla, igual que el resto de los
    /// servicios del proyecto — una ficha se abre de a una, y ninguna fuente de datos ofrece un
    /// <c>GetByArticulo</c>.
    ///
    /// La existencia se deriva de lo que la propia ficha juntó (entradas − salidas, los mismos
    /// documentos que cuentan para <c>InventarioService</c>/<c>MateriaPrimaService</c>) en vez de
    /// recibirse ya calculada: así el número de la cabecera y la suma de los saldos por compra no
    /// pueden discrepar.
    /// </summary>
    private FichaMaterial Ficha(OrigenMaterial origen,
                                int materialId,
                                string nombre,
                                string unidad,
                                decimal minimo,
                                string estadoTexto,
                                string codigo = "")
    {
        var compras = origen == OrigenMaterial.Articulo
            ? ComprasDeArticulo(materialId, unidad)
            : ComprasDeMateriaPrima(materialId, unidad);

        var salidas = origen == OrigenMaterial.Articulo
            ? SalidasDeArticulo(materialId, unidad)
            : SalidasDeMateriaPrima(materialId, unidad);

        RepartirFifo(compras, salidas.Sum(s => s.Cantidad));

        return new FichaMaterial
        {
            Origen = origen,
            MaterialId = materialId,
            Nombre = nombre,
            Codigo = codigo,
            Unidad = unidad,
            Existencia = compras.Sum(c => c.Cantidad) - salidas.Sum(s => s.Cantidad),
            Minimo = minimo,
            EstadoTexto = estadoTexto,
            Compras = compras,
            Salidas = salidas
        };
    }

    /// <summary>
    /// Una salida no dice de qué compra salió, así que se imputa a la más vieja primero, hasta
    /// agotarla, y se sigue con la siguiente — mismo criterio con el que
    /// <c>ProductosService.Lotes()</c> reparte los despachos que no citan lote.
    ///
    /// Se reparte el total consumido de una vez, sin mirar la fecha de cada salida: en FIFO puro el
    /// resultado es el mismo, y hacerlo salida por salida solo abriría la puerta a un negativo
    /// intermedio si una salida quedó fechada antes que la compra que la surtió.
    ///
    /// Como existencia = Σ entradas − Σ salidas, la suma de los <c>Restante</c> da exactamente la
    /// existencia. Si un dato corrupto dejara más salidas que entradas, el sobrante se descarta:
    /// las reglas de <c>SalidasInventarioService</c>/<c>SalidasMateriaPrimaService</c> ya impiden
    /// registrar una salida que deje la existencia en negativo.
    /// </summary>
    private static void RepartirFifo(List<CompraMaterial> compras, decimal consumido)
    {
        var pendiente = consumido;

        foreach (var compra in compras)
        {
            if (pendiente <= 0)
                break;

            compra.Consumido = Math.Min(pendiente, compra.Cantidad);
            pendiente -= compra.Consumido;
        }
    }

    private List<CompraMaterial> ComprasDeArticulo(int articuloId, string unidad) =>
        [.. _entradas.GetAll()
            .Where(e => e.CuentaEnKardex)
            .SelectMany(e => e.Lineas
                .Where(l => l.ArticuloId == articuloId)
                .Select(l => new CompraMaterial
                {
                    Fecha = e.Fecha,
                    Numero = e.Numero,
                    OrigenTexto = e.OrigenTexto,
                    DocumentoTexto = e.NumeroDocumento,
                    Unidad = string.IsNullOrWhiteSpace(l.UnidadTexto) ? unidad : l.UnidadTexto,
                    Cantidad = l.Cantidad,
                    PrecioUnitario = l.PrecioUnitario
                }))
            .OrderBy(c => c.Fecha)
            .ThenBy(c => c.Numero, StringComparer.Ordinal)];

    private List<CompraMaterial> ComprasDeMateriaPrima(int tipoId, string unidad) =>
        [.. _recepciones.GetAll()
            .Where(r => r.CuentaEnExistencia)
            .SelectMany(r => r.Lineas
                .Where(l => l.TipoMateriaPrimaId == tipoId)
                .Select(l => new CompraMaterial
                {
                    Fecha = r.Fecha,
                    Numero = r.Numero,
                    OrigenTexto = r.OrigenTexto,
                    DocumentoTexto = string.IsNullOrWhiteSpace(r.NumeroDocumento) ? r.Referencia : r.NumeroDocumento,
                    Unidad = string.IsNullOrWhiteSpace(l.UnidadMedidaSnapshot) ? unidad : l.UnidadMedidaSnapshot,
                    Cantidad = l.Cantidad,
                    PrecioUnitario = l.PrecioUnitario
                }))
            .OrderBy(c => c.Fecha)
            .ThenBy(c => c.Numero, StringComparer.Ordinal)];

    private List<ConsumoMaterial> SalidasDeArticulo(int articuloId, string unidad) =>
        [.. _salidasInventario.GetAll()
            .Where(s => s.CuentaEnKardex)
            .SelectMany(s => s.Lineas
                .Where(l => l.ArticuloId == articuloId)
                .Select(l => new ConsumoMaterial
                {
                    Fecha = s.Fecha,
                    Numero = s.Numero,
                    Unidad = string.IsNullOrWhiteSpace(l.UnidadTexto) ? unidad : l.UnidadTexto,
                    Cantidad = l.Cantidad,
                    MotivoTexto = s.MotivoDetalleTexto
                }))
            .OrderByDescending(s => s.Fecha)
            .ThenByDescending(s => s.Numero, StringComparer.Ordinal)];

    private List<ConsumoMaterial> SalidasDeMateriaPrima(int tipoId, string unidad) =>
        [.. _salidasMateriaPrima.GetAll()
            .Where(s => s.CuentaEnExistencia)
            .SelectMany(s => s.Lineas
                .Where(l => l.TipoMateriaPrimaId == tipoId)
                .Select(l => new ConsumoMaterial
                {
                    Fecha = s.Fecha,
                    Numero = s.Numero,
                    Unidad = string.IsNullOrWhiteSpace(l.UnidadMedidaSnapshot) ? unidad : l.UnidadMedidaSnapshot,
                    Cantidad = l.Cantidad,
                    MotivoTexto = s.MotivoDetalleTexto
                }))
            .OrderByDescending(s => s.Fecha)
            .ThenByDescending(s => s.Numero, StringComparer.Ordinal)];
}
