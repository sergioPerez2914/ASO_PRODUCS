using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>Un renglón de lo que va a consumir una transformación, con lo que hay para cubrirlo.</summary>
public sealed class ConsumoPlaneado
{
    public OrigenMaterial Origen { get; init; }
    public int MaterialId { get; init; }
    public string MaterialNombre { get; init; } = string.Empty;
    public string Unidad { get; init; } = string.Empty;
    public decimal Cantidad { get; init; }
    public decimal Disponible { get; init; }
    public int? LoteProcesoId { get; init; }
    public string? LoteNumero { get; init; }

    public bool Alcanza => Disponible >= Cantidad;

    public string OrigenTexto => Origen == OrigenMaterial.Producto && LoteProcesoId is null
        ? "Sin lote"
        : OrigenMaterialTexto.De(Origen, LoteNumero);

    public string CantidadTexto => $"{Cantidad:N2} {Unidad}".Trim();

    public string DisponibleTexto => Alcanza
        ? $"Quedan {Disponible - Cantidad:N2} {Unidad}".Trim()
        : $"Faltan {Cantidad - Disponible:N2} {Unidad}".Trim();
}

/// <summary>
/// Transformar un producto base en una presentación o subproducto (Mantequilla → Mantequilla
/// 200 g, Ghee). No es un documento nuevo: arma un <see cref="ProcesoProduccion"/> normal a partir
/// de la receta del producto derivado (<see cref="Producto.ProductoBaseId"/>,
/// <see cref="Producto.CantidadBasePorUnidad"/>, <see cref="Producto.Componentes"/>) y lo pasa por
/// <see cref="ProcesosProduccionService"/>, que es quien valida, numera y descuenta.
/// </summary>
public sealed class TransformacionesService
{
    private readonly ProcesosProduccionService _procesos;
    private readonly ProductosService _productos;
    private readonly MateriaPrimaService _materiaPrima;
    private readonly InventarioService _inventario;

    public TransformacionesService(ProcesosProduccionService procesos,
                                   ProductosService productos,
                                   MateriaPrimaService materiaPrima,
                                   InventarioService inventario)
    {
        _procesos = procesos;
        _productos = productos;
        _materiaPrima = materiaPrima;
        _inventario = inventario;
    }

    public IReadOnlyList<Producto> ProductosDerivados() => _productos.DerivadosActivos();

    /// <summary>Catálogos para el combo de origen del consumo adicional del editor (Materia prima,
    /// Inventario, Producto): el editor no tiene por qué conocer <see cref="MateriaPrimaService"/>/
    /// <see cref="InventarioService"/> de por sí, ya están inyectados aquí.</summary>
    public IReadOnlyList<TipoMateriaPrima> TiposMateriaPrima() => _materiaPrima.TiposActivos();

    public IReadOnlyList<Articulo> Articulos() => _inventario.ArticulosActivos();

    /// <summary>Foto de las existencias que usa <see cref="Planificar"/>: el formulario la toma una
    /// vez al abrir para que la vista previa no relea la base en cada tecla. La transformación
    /// real toma una nueva, y el servicio de procesos vuelve a validar en vivo.</summary>
    public sealed record Existencias(
        IReadOnlyList<LoteProducto> Lotes,
        IReadOnlyDictionary<int, decimal> MateriaPrima,
        IReadOnlyDictionary<int, decimal> Inventario);

    public Existencias TomarExistencias() =>
        new(_productos.LotesConExistencia(), _materiaPrima.ExistenciasPorTipo(), _inventario.ExistenciasPorArticulo());

    /// <summary>Lotes con existencia del producto base, en orden FEFO.</summary>
    public static IReadOnlyList<LoteProducto> LotesDisponibles(Producto derivado, Existencias existencias) =>
        [.. existencias.Lotes.Where(l => l.ProductoId == derivado.ProductoBaseId)];

    /// <summary>
    /// Qué va a consumir producir <paramref name="unidades"/> de <paramref name="derivado"/>. El
    /// producto base sale primero de <paramref name="loteId"/> y, si no alcanza, de los demás lotes
    /// en orden FEFO. Lo que no se cubre queda como un renglón que no alcanza — no se lanza nada:
    /// es la vista previa del formulario.
    /// </summary>
    public static IReadOnlyList<ConsumoPlaneado> Planificar(Producto derivado, decimal unidades, int? loteId,
                                                            Existencias existencias)
    {
        var plan = new List<ConsumoPlaneado>();

        if (unidades <= 0 || derivado.ProductoBaseId is not { } baseId)
            return plan;

        var pendiente = unidades * (derivado.CantidadBasePorUnidad ?? 0);

        var lotes = LotesDisponibles(derivado, existencias)
            .OrderBy(l => l.ProcesoId == loteId ? 0 : 1)
            .ToList();

        var nombreBase = derivado.ProductoBaseNombre;

        foreach (var lote in lotes)
        {
            if (pendiente <= 0)
                break;

            var tomado = Math.Min(pendiente, lote.Existencia);
            plan.Add(new ConsumoPlaneado
            {
                Origen = OrigenMaterial.Producto,
                MaterialId = baseId,
                MaterialNombre = nombreBase,
                Unidad = lote.Unidad,
                Cantidad = tomado,
                Disponible = lote.Existencia,
                LoteProcesoId = lote.ProcesoId,
                LoteNumero = lote.Numero
            });
            pendiente -= tomado;
        }

        if (pendiente > 0)
            plan.Add(new ConsumoPlaneado
            {
                Origen = OrigenMaterial.Producto,
                MaterialId = baseId,
                MaterialNombre = nombreBase,
                Unidad = lotes.FirstOrDefault()?.Unidad ?? string.Empty,
                Cantidad = pendiente,
                Disponible = 0
            });

        var existenciasMateriaPrima = existencias.MateriaPrima;
        var existenciasInventario = existencias.Inventario;

        foreach (var componente in derivado.Componentes)
            plan.Add(new ConsumoPlaneado
            {
                Origen = componente.Origen,
                MaterialId = componente.MaterialId,
                MaterialNombre = componente.MaterialNombre,
                Unidad = componente.UnidadMedidaSnapshot,
                Cantidad = unidades * componente.CantidadPorUnidad,
                Disponible = componente.Origen == OrigenMaterial.MateriaPrima
                    ? existenciasMateriaPrima.GetValueOrDefault(componente.MaterialId)
                    : existenciasInventario.GetValueOrDefault(componente.MaterialId)
            });

        return plan;
    }

    /// <summary>
    /// Inicia el proceso de transformación con el consumo planeado y, si
    /// <paramref name="terminarAhora"/>, lo termina en el mismo paso (un fraccionado no tiene
    /// etapas). Si no, queda En proceso para seguir por etapas como cualquier otro.
    ///
    /// <paramref name="extra"/> es lo que el operador agregó a mano en el formulario —merma,
    /// consumo no previsto en la receta—, mismo mecanismo que la grilla de líneas manuales de
    /// "Iniciar proceso". Se funde con lo que propone la receta antes de pasarlo a
    /// <see cref="ProcesosProduccionService"/>: ver <see cref="Fundir"/>.
    /// </summary>
    public ProcesoProduccion Transformar(Producto derivado, decimal unidades, int? loteId, bool terminarAhora,
                                         DateTime? fechaVencimiento, string observaciones, int usuarioId,
                                         IReadOnlyList<ProcesoProduccionLineaInicial>? extra = null)
    {
        if (!derivado.EsDerivado)
            throw new InvalidOperationException($"{derivado.Nombre} no está configurado como presentación de otro producto.");

        if (unidades <= 0)
            throw new InvalidOperationException("Indique cuánto va a producir, con un número mayor que cero.");

        var plan = Planificar(derivado, unidades, loteId, TomarExistencias());

        if (plan.FirstOrDefault(c => !c.Alcanza) is { } falta)
            throw new InvalidOperationException($"No alcanza {falta.MaterialNombre}: {falta.DisponibleTexto.ToLowerInvariant()}.");

        var proceso = new ProcesoProduccion
        {
            Fecha = DateTime.Today,
            ProductoId = derivado.Id,
            ProductoNombre = derivado.Nombre,
            UnidadMedidaSnapshot = derivado.UnidadMedida,
            CantidadPlaneada = unidades,
            Observaciones = observaciones.Trim(),
            LineasIniciales = Fundir(plan, extra ?? [])
        };

        var iniciado = _procesos.Iniciar(proceso, usuarioId);

        return terminarAhora
            ? _procesos.Terminar(iniciado, unidades, fechaVencimiento, new EtapaProcesoProduccion(), usuarioId)
            : iniciado;
    }

    /// <summary>
    /// Junta la receta con lo agregado a mano, sumando cantidades cuando coinciden en
    /// <c>(Origen, MaterialId, LoteProcesoId)</c> — sin esto, agregar a mano un Pote extra cuando
    /// la receta ya propone uno choca con la guarda de línea duplicada de
    /// <c>ProcesosProduccionService.ValidarLineas</c> ("está en más de una línea con el mismo
    /// motivo"), que tiene sentido para dos líneas cargadas a mano por error pero no para la
    /// receta (que el operador no ve en esta misma grilla) más un extra legítimo del mismo
    /// material.
    /// </summary>
    private static List<ProcesoProduccionLineaInicial> Fundir(IReadOnlyList<ConsumoPlaneado> plan,
                                                               IReadOnlyList<ProcesoProduccionLineaInicial> extra)
    {
        var todas = plan
            .Select(c => new ProcesoProduccionLineaInicial
            {
                Origen = c.Origen,
                MaterialId = c.MaterialId,
                MaterialNombre = c.MaterialNombre,
                UnidadMedidaSnapshot = c.Unidad,
                Cantidad = c.Cantidad,
                LoteProcesoId = c.LoteProcesoId,
                LoteNumero = c.LoteNumero
            })
            .Concat(extra);

        return [.. todas
            .GroupBy(l => (l.Origen, l.MaterialId, l.LoteProcesoId))
            .Select(g => g.Count() == 1 ? g.First() : new ProcesoProduccionLineaInicial
            {
                Origen = g.Key.Origen,
                MaterialId = g.Key.MaterialId,
                MaterialNombre = g.First().MaterialNombre,
                UnidadMedidaSnapshot = g.First().UnidadMedidaSnapshot,
                Cantidad = g.Sum(l => l.Cantidad),
                LoteProcesoId = g.Key.LoteProcesoId,
                LoteNumero = g.First().LoteNumero
            })];
    }
}
