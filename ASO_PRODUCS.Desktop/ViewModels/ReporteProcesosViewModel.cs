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

    private readonly IProcesoProduccionDataSource _procesos;
    private readonly ISalidaMateriaPrimaDataSource _salidasMateriaPrima;
    private readonly ISalidaInventarioDataSource _salidasInventario;
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
    public ObservableCollection<FilaProduccionPorProducto> Produccion { get; } = [];
    public ObservableCollection<FilaConsumoInsumo> Consumo { get; } = [];

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

        var porProducto = procesosEnRango
            .GroupBy(p => (p.ProductoId, p.ProductoNombre, p.UnidadMedidaSnapshot))
            .Select(g => new FilaProduccionPorProducto(
                g.Key.ProductoNombre,
                g.Key.UnidadMedidaSnapshot,
                g.Sum(p => p.CantidadPlaneada),
                g.Sum(p => p.CantidadProducida ?? 0),
                g.Count()))
            .OrderByDescending(f => f.CantidadProducida)
            .ToList();

        Produccion.Clear();
        foreach (var fila in porProducto)
            Produccion.Add(fila);

        var totalPlaneada = porProducto.Sum(f => f.CantidadPlaneada);
        var totalProducida = porProducto.Sum(f => f.CantidadProducida);
        var rendimiento = totalPlaneada > 0 ? totalProducida / totalPlaneada * 100 : 0;

        var idsProcesos = procesosEnRango.Select(p => p.Id).ToHashSet();

        var consumoMateriaPrima = _salidasMateriaPrima.GetAll()
            .Where(s => s.Estado == EstadoSalidaMateriaPrima.Registrada
                        && s.ProcesoProduccionId is { } id && idsProcesos.Contains(id))
            .SelectMany(s => s.Lineas)
            .GroupBy(l => (l.TipoMateriaPrimaNombre, l.UnidadMedidaSnapshot))
            .Select(g => new FilaConsumoInsumo("Materia prima", g.Key.TipoMateriaPrimaNombre,
                g.Key.UnidadMedidaSnapshot, g.Sum(l => l.Cantidad)));

        var consumoInventario = _salidasInventario.GetAll()
            .Where(s => s.Estado == EstadoSalida.Registrada
                        && s.ProcesoProduccionId is { } id && idsProcesos.Contains(id))
            .SelectMany(s => s.Lineas)
            .GroupBy(l => (l.ArticuloNombre, l.UnidadTexto))
            .Select(g => new FilaConsumoInsumo("Inventario", g.Key.ArticuloNombre,
                g.Key.UnidadTexto, g.Sum(l => l.Cantidad)));

        var consumo = consumoMateriaPrima.Concat(consumoInventario)
            .OrderBy(f => f.Origen).ThenByDescending(f => f.Cantidad)
            .ToList();

        Consumo.Clear();
        foreach (var fila in consumo)
            Consumo.Add(fila);

        Indicadores.Clear();
        Indicadores.Add(new Indicador("Procesos terminados", terminados.ToString(), "en el período"));
        Indicadores.Add(new Indicador("Rendimiento promedio", $"{rendimiento:N1}%", "producido sobre lo planeado",
            totalPlaneada > 0 && rendimiento < 90 ? EstadoIndicador.Atencion : EstadoIndicador.Normal));
        Indicadores.Add(new Indicador("Productos fabricados", porProducto.Count.ToString(), "productos distintos en el período"));
    }

    private void ExportarExcel()
    {
        var ruta = _dialogos.GuardarArchivo("Exportar reporte de procesos", "ReporteProcesos.xlsx",
            "Libro de Excel (*.xlsx)|*.xlsx");

        if (ruta is null)
            return;

        ExportadorExcel.Exportar(ruta,
        [
            new HojaExcel("Producción",
                ["Producto", "Unidad", "Cantidad planeada", "Cantidad producida", "Rendimiento %", "Procesos"],
                Produccion.Select(f => (IReadOnlyList<string>)
                [
                    f.ProductoNombre, f.UnidadMedida, f.CantidadPlaneadaTexto, f.CantidadProducidaTexto,
                    f.RendimientoTexto, f.CantidadProcesos.ToString()
                ]).ToList()),
            new HojaExcel("Consumo de insumos",
                ["Origen", "Material", "Unidad", "Cantidad consumida"],
                Consumo.Select(f => (IReadOnlyList<string>)
                [
                    f.Origen, f.MaterialNombre, f.Unidad, f.CantidadTexto
                ]).ToList())
        ]);

        _dialogos.Informar("Reporte exportado", $"El archivo se guardó en:\n{ruta}");
    }
}

/// <summary>Fila del desglose "Producción por producto".</summary>
public sealed record FilaProduccionPorProducto(string ProductoNombre, string UnidadMedida,
    decimal CantidadPlaneada, decimal CantidadProducida, int CantidadProcesos)
{
    public string CantidadPlaneadaTexto => $"{CantidadPlaneada:N2} {UnidadMedida}".Trim();
    public string CantidadProducidaTexto => $"{CantidadProducida:N2} {UnidadMedida}".Trim();
    public decimal Rendimiento => CantidadPlaneada > 0 ? CantidadProducida / CantidadPlaneada * 100 : 0;
    public string RendimientoTexto => $"{Rendimiento:N1}%";
}

/// <summary>Fila del desglose "Consumo de insumos": Origen es "Materia prima" o "Inventario".</summary>
public sealed record FilaConsumoInsumo(string Origen, string MaterialNombre, string Unidad, decimal Cantidad)
{
    public string CantidadTexto => $"{Cantidad:N2} {Unidad}".Trim();
}
