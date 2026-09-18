using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Controls;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Reportes · Procesos: producción, rendimiento y consumo de insumos por período.
///
/// De solo lectura: no hay alta/edición/borrado, así que hereda de <see cref="PantallaViewModelBase"/>
/// (no de <see cref="PantallaCrudViewModel{T, TId}"/>) y todo el cálculo es LINQ en memoria sobre
/// los <c>IXDataSource</c> ya existentes — mismo criterio que ya usa
/// <see cref="ModuloDashboardViewModel.CalcularProcesos"/>. Ningún dato nuevo, ninguna migración.
/// </summary>
public sealed class ReporteProcesosViewModel : PantallaViewModelBase
{
    public const string VistaProduccion = "Produccion";
    public const string VistaConsumo = "Consumo";
    public const string VistaCostos = "Costos";

    private readonly IProcesoProduccionDataSource _procesos;
    private readonly ISalidaMateriaPrimaDataSource _salidasMateriaPrima;
    private readonly ISalidaInventarioDataSource _salidasInventario;
    private readonly CostosProduccionService _costos;
    private readonly IServicioDialogo _dialogos;

    public ReporteProcesosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearProcesosProduccion(),
               DataSourceFactory.CrearSalidasMateriaPrima(), DataSourceFactory.CrearSalidasInventario(),
               new ServicioDialogo())
    {
    }

    private ReporteProcesosViewModel(Modulo modulo,
                                     Submodulo submodulo,
                                     IProcesoProduccionDataSource procesos,
                                     ISalidaMateriaPrimaDataSource salidasMateriaPrima,
                                     ISalidaInventarioDataSource salidasInventario,
                                     IServicioDialogo dialogos)
        : base(modulo, submodulo)
    {
        _procesos = procesos;
        _salidasMateriaPrima = salidasMateriaPrima;
        _salidasInventario = salidasInventario;
        _costos = new CostosProduccionService(DataSourceFactory.CrearRecepcionesMateriaPrima(),
            DataSourceFactory.CrearEntradasInventario(), salidasMateriaPrima, salidasInventario,
            DataSourceFactory.CrearProductos(), procesos);
        _dialogos = dialogos;

        _fechaDesde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _fechaHasta = DateTime.Today;

        ExportarExcelCommand = new RelayCommand(ExportarExcel);

        Recalcular();
    }

    public ICommand ExportarExcelCommand { get; }

    private DateTime _fechaDesde;
    public DateTime FechaDesde
    {
        get => _fechaDesde;
        set { if (SetProperty(ref _fechaDesde, value)) Recalcular(); }
    }

    private DateTime _fechaHasta;
    public DateTime FechaHasta
    {
        get => _fechaHasta;
        set { if (SetProperty(ref _fechaHasta, value)) Recalcular(); }
    }

    public ObservableCollection<Indicador> Indicadores { get; } = [];
    public ObservableCollection<FilaProduccionPorDia> Produccion { get; } = [];
    public ObservableCollection<FilaConsumoInsumo> Consumo { get; } = [];
    public ObservableCollection<FilaCostoProceso> Costos { get; } = [];

    private string _vistaActual = VistaProduccion;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarProduccion => VistaActual == VistaProduccion;
    public bool MostrarConsumo => VistaActual == VistaConsumo;
    public bool MostrarCostos => VistaActual == VistaCostos;

    public bool EsCostos
    {
        get => MostrarCostos;
        set { if (value) VistaActual = VistaCostos; }
    }

    public bool EsProduccion
    {
        get => MostrarProduccion;
        set { if (value) VistaActual = VistaProduccion; }
    }

    public bool EsConsumo
    {
        get => MostrarConsumo;
        set { if (value) VistaActual = VistaConsumo; }
    }

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var desde = FechaDesde.Date;
        var hasta = FechaHasta.Date;

        var procesosEnRango = _procesos.GetAll()
            .Where(p => p.Estado != EstadoProcesoProduccion.Anulado
                        && p.Fecha.Date >= desde && p.Fecha.Date <= hasta)
            .ToList();

        var terminados = procesosEnRango.Count(p => p.Estado == EstadoProcesoProduccion.Terminado);

        // Se agrupa por FechaTermino (cuándo se supo cuánto se produjo de verdad), no por Fecha de
        // inicio: un proceso EnProceso no tiene FechaTermino y no aparece en esta tabla hasta que
        // termine. Se agrupa también por producto+unidad: sumar cantidades de productos con
        // distinta unidad en una sola fila daría un total sin sentido.
        var porDia = procesosEnRango
            .Where(p => p.Estado == EstadoProcesoProduccion.Terminado && p.FechaTermino is not null)
            .GroupBy(p => (Fecha: p.FechaTermino!.Value.Date, p.ProductoNombre, p.UnidadMedidaSnapshot))
            .Select(g => new FilaProduccionPorDia(g.Key.Fecha, g.Key.ProductoNombre, g.Key.UnidadMedidaSnapshot,
                g.Sum(p => p.CantidadPlaneada), g.Sum(p => p.CantidadProducida ?? 0)))
            .OrderBy(f => f.Fecha).ThenByDescending(f => f.CantidadProducida)
            .ToList();

        Produccion.Clear();
        foreach (var fila in porDia)
            Produccion.Add(fila);

        var totalPlaneada = procesosEnRango.Sum(p => p.CantidadPlaneada);
        var totalProducida = procesosEnRango.Sum(p => p.CantidadProducida ?? 0);
        var rendimiento = totalPlaneada > 0 ? totalProducida / totalPlaneada * 100 : 0;
        var productosFabricados = procesosEnRango.Select(p => p.ProductoId).Distinct().Count();

        var idsProcesos = procesosEnRango.Select(p => p.Id).ToHashSet();

        // Las líneas no tienen fecha propia: se toma la Fecha del encabezado de la salida (cuándo
        // salió el material de verdad), y se agrupa también por día — mismo motivo que
        // "Producción": sin fecha no se sabe en qué día se consumieron esos insumos.
        var consumoMateriaPrima = _salidasMateriaPrima.GetAll()
            .Where(s => s.Estado == EstadoSalidaMateriaPrima.Registrada
                        && s.ProcesoProduccionId is { } id && idsProcesos.Contains(id))
            .SelectMany(s => s.Lineas.Select(l => (s.Fecha, l.TipoMateriaPrimaNombre, l.UnidadMedidaSnapshot, l.Cantidad)))
            .GroupBy(x => (x.Fecha.Date, x.TipoMateriaPrimaNombre, x.UnidadMedidaSnapshot))
            .Select(g => new FilaConsumoInsumo(g.Key.Date, "Materia prima", g.Key.TipoMateriaPrimaNombre,
                g.Key.UnidadMedidaSnapshot, g.Sum(x => x.Cantidad)));

        var consumoInventario = _salidasInventario.GetAll()
            .Where(s => s.Estado == EstadoSalida.Registrada
                        && s.ProcesoProduccionId is { } id && idsProcesos.Contains(id))
            .SelectMany(s => s.Lineas.Select(l => (s.Fecha, l.ArticuloNombre, l.UnidadTexto, l.Cantidad)))
            .GroupBy(x => (x.Fecha.Date, x.ArticuloNombre, x.UnidadTexto))
            .Select(g => new FilaConsumoInsumo(g.Key.Date, "Inventario", g.Key.ArticuloNombre,
                g.Key.UnidadTexto, g.Sum(x => x.Cantidad)));

        var consumo = consumoMateriaPrima.Concat(consumoInventario)
            .OrderBy(f => f.Fecha).ThenBy(f => f.Origen).ThenByDescending(f => f.Cantidad)
            .ToList();

        Consumo.Clear();
        foreach (var fila in consumo)
            Consumo.Add(fila);

        // Solo Terminados: sin cantidad producida no hay costo por unidad ni margen que comparar.
        var procesosTerminados = procesosEnRango
            .Where(p => p.Estado == EstadoProcesoProduccion.Terminado)
            .ToList();
        var costos = _costos.Calcular(procesosTerminados);

        Costos.Clear();
        foreach (var proceso in procesosTerminados.OrderBy(p => p.FechaTermino).ThenBy(p => p.Numero))
            Costos.Add(new FilaCostoProceso(proceso, costos[proceso.Id]));

        Indicadores.Clear();
        Indicadores.Add(new Indicador("Procesos terminados", terminados.ToString(), "en el período"));
        Indicadores.Add(new Indicador("Rendimiento promedio", $"{rendimiento:N1}%", "producido sobre lo planeado",
            totalPlaneada > 0 && rendimiento < 90 ? EstadoIndicador.Atencion : EstadoIndicador.Normal));
        Indicadores.Add(new Indicador("Productos fabricados", productosFabricados.ToString(), "productos distintos en el período"));
    }

    /// <summary>Exporta SOLO la vista abierta en pantalla — mismo criterio que
    /// <see cref="ReporteVentasViewModel.ExportarExcel"/>.</summary>
    private void ExportarExcel()
    {
        string nombreVista;
        IReadOnlyList<string> encabezados;
        IReadOnlyList<IReadOnlyList<string>> filas;

        if (VistaActual == VistaCostos)
        {
            nombreVista = "Costos";
            encabezados = ["Fecha término", "Nº", "Producto", "Producida", "Costo total", "Costo por unidad",
                           "Precio", "Margen %", "Costo incompleto"];
            filas = Costos.Select(f => (IReadOnlyList<string>)
                [f.FechaTexto, f.Numero, f.ProductoNombre, f.CantidadProducidaTexto, f.Costo.CostoTotalTexto,
                 f.Costo.CostoPorUnidadTexto, f.Costo.PrecioVentaTexto, f.Costo.MargenPorcentajeTexto, f.IncompletoTexto]).ToList();
        }
        else if (VistaActual == VistaConsumo)
        {
            nombreVista = "Consumo de insumos";
            encabezados = ["Fecha", "Origen", "Material", "Unidad", "Cantidad consumida"];
            filas = Consumo.Select(f => (IReadOnlyList<string>)
                [f.FechaTexto, f.Origen, f.MaterialNombre, f.Unidad, f.CantidadTexto]).ToList();
        }
        else
        {
            nombreVista = "Producción";
            encabezados = ["Fecha", "Producto", "Planeada", "Producida", "Rendimiento %"];
            filas = Produccion.Select(f => (IReadOnlyList<string>)
                [f.FechaTexto, f.ProductoNombre, f.CantidadPlaneadaTexto, f.CantidadProducidaTexto, f.RendimientoTexto]).ToList();
        }

        var ruta = _dialogos.GuardarArchivo("Exportar reporte de procesos",
            $"ReporteProcesos_{nombreVista.Replace(" ", "")}.xlsx", "Libro de Excel (*.xlsx)|*.xlsx");

        if (ruta is null)
            return;

        ExportadorExcel.Exportar(ruta,
        [
            new HojaExcel(nombreVista, encabezados, filas,
                Titulo: $"{Submodulo?.Nombre ?? "Reporte de Procesos"} · {nombreVista}",
                Periodo: $"Período: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}")
        ]);

        Aviso.Mostrar($"Reporte exportado a {System.IO.Path.GetFileName(ruta)}");
    }
}

/// <summary>Fila de la pestaña "Costos": un proceso Terminado con su costo de materiales y margen.</summary>
public sealed record FilaCostoProceso(ProcesoProduccion Proceso, CostoProceso Costo)
{
    public string FechaTexto => Proceso.FechaTermino?.ToString("dd/MM/yyyy") ?? string.Empty;
    public string Numero => Proceso.Numero;
    public string ProductoNombre => Proceso.ProductoNombre;
    public string CantidadProducidaTexto => Proceso.CantidadProducidaTexto;
    public string IncompletoTexto => Costo.Incompleto ? "Sí" : "No";
}

/// <summary>Fila del desglose "Consumo de insumos": un día + un material (Origen es "Materia
/// prima" o "Inventario"), tomando la Fecha del encabezado de la salida que lo consumió.</summary>
public sealed record FilaConsumoInsumo(DateTime Fecha, string Origen, string MaterialNombre, string Unidad, decimal Cantidad)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string CantidadTexto => $"{Cantidad:N2} {Unidad}".Trim();
}

/// <summary>Fila del desglose "Producción": un día + un producto (procesos Terminados agrupados
/// por FechaTermino), para ver el rendimiento día a día en vez de un solo total por período.</summary>
public sealed record FilaProduccionPorDia(DateTime Fecha, string ProductoNombre, string UnidadMedida,
    decimal CantidadPlaneada, decimal CantidadProducida)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string CantidadPlaneadaTexto => $"{CantidadPlaneada:N2} {UnidadMedida}".Trim();
    public string CantidadProducidaTexto => $"{CantidadProducida:N2} {UnidadMedida}".Trim();
    public decimal Rendimiento => CantidadPlaneada > 0 ? CantidadProducida / CantidadPlaneada * 100 : 0;
    public string RendimientoTexto => $"{Rendimiento:N1}%";
}
