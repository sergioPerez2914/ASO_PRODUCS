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
/// Reportes · Ventas: lo vendido, cobrado y pendiente por período.
///
/// De solo lectura, igual que <see cref="ReporteProcesosViewModel"/>: hereda de
/// <see cref="PantallaViewModelBase"/> y se arma con <see cref="Despacho"/> tipo Venta y sus
/// <see cref="FacturaCliente"/>, sin ningún dato ni migración nueva.
/// </summary>
public sealed class ReporteVentasViewModel : PantallaViewModelBase
{
    public const string VistaPorProducto = "PorProducto";
    public const string VistaPorCliente = "PorCliente";
    public const string VistaDetalle = "Detalle";

    private readonly IDespachoDataSource _despachos;
    private readonly IFacturaClienteDataSource _facturasCliente;
    private readonly IServicioDialogo _dialogos;

    public ReporteVentasViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearDespachos(), DataSourceFactory.CrearFacturasCliente(),
               new ServicioDialogo())
    {
    }

    private ReporteVentasViewModel(Modulo modulo,
                                   Submodulo submodulo,
                                   IDespachoDataSource despachos,
                                   IFacturaClienteDataSource facturasCliente,
                                   IServicioDialogo dialogos)
        : base(modulo, submodulo)
    {
        _despachos = despachos;
        _facturasCliente = facturasCliente;
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
    public ObservableCollection<FilaVentaPorProducto> PorProducto { get; } = [];
    public ObservableCollection<FilaVentaPorCliente> PorCliente { get; } = [];
    public ObservableCollection<FilaVentaDetalle> Detalle { get; } = [];

    private string _vistaActual = VistaPorProducto;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarPorProducto => VistaActual == VistaPorProducto;
    public bool MostrarPorCliente => VistaActual == VistaPorCliente;
    public bool MostrarDetalle => VistaActual == VistaDetalle;

    public bool EsPorProducto
    {
        get => MostrarPorProducto;
        set { if (value) VistaActual = VistaPorProducto; }
    }

    public bool EsPorCliente
    {
        get => MostrarPorCliente;
        set { if (value) VistaActual = VistaPorCliente; }
    }

    public bool EsDetalle
    {
        get => MostrarDetalle;
        set { if (value) VistaActual = VistaDetalle; }
    }

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var desde = FechaDesde.Date;
        var hasta = FechaHasta.Date;

        var despachosEnRango = _despachos.GetAll()
            .Where(d => d.Tipo == TipoDespacho.Venta && d.Estado == EstadoDespacho.Registrado
                        && d.Fecha.Date >= desde && d.Fecha.Date <= hasta)
            .ToList();

        var totalVendido = despachosEnRango.Sum(d => d.Total);

        // Agrupa también por unidad, mismo criterio que ReporteProcesosViewModel: dos productos
        // con el mismo nombre pero distinta unidad no deberían sumarse en una sola fila.
        var porProducto = despachosEnRango
            .SelectMany(d => d.Lineas)
            .GroupBy(l => (l.ProductoNombre, l.UnidadMedidaSnapshot))
            .Select(g => new FilaVentaPorProducto(g.Key.ProductoNombre, g.Key.UnidadMedidaSnapshot,
                g.Sum(l => l.Cantidad), g.Sum(l => l.Subtotal)))
            .OrderByDescending(f => f.Monto)
            .ToList();

        PorProducto.Clear();
        foreach (var fila in porProducto)
            PorProducto.Add(fila);

        var porCliente = despachosEnRango
            .GroupBy(d => d.ClienteNombre)
            .Select(g => new FilaVentaPorCliente(g.Key, g.Count(), g.Sum(d => d.Total)))
            .OrderByDescending(f => f.Monto)
            .ToList();

        PorCliente.Clear();
        foreach (var fila in porCliente)
            PorCliente.Add(fila);

        // Sin agrupar: una fila por cada línea de despacho, con su cliente, su unidad y el
        // proceso de producción que la originó (si se indicó al despachar) — es lo que hace
        // falta para rastrear una venta exacta, en vez de un total agregado.
        var detalle = despachosEnRango
            .SelectMany(d => d.Lineas.Select(l => new FilaVentaDetalle(
                d.Fecha, d.Numero, d.ClienteNombre, l.ProductoNombre,
                $"{l.Cantidad:N2} {l.UnidadMedidaSnapshot}".Trim(),
                l.PrecioUnitario, l.Subtotal, l.ProcesoOrigenTexto)))
            .OrderByDescending(f => f.Fecha)
            .ToList();

        Detalle.Clear();
        foreach (var fila in detalle)
            Detalle.Add(fila);

        // El estado de cobro se mira contra las facturas EMITIDAS en el período, no contra los
        // despachos: una factura de alta manual también cuenta y un despacho no dice por sí solo
        // si ya se cobró.
        var facturasEnRango = _facturasCliente.GetAll()
            .Where(f => f.Estado != EstadoFacturaCliente.Anulada
                        && f.FechaEmision.Date >= desde && f.FechaEmision.Date <= hasta)
            .ToList();

        var cobrado = facturasEnRango.Where(f => f.Estado == EstadoFacturaCliente.Cobrada).Sum(f => f.Monto);
        var vencido = facturasEnRango.Where(f => f.EstaVencida).Sum(f => f.Monto);
        var pendiente = facturasEnRango
            .Where(f => f.Estado == EstadoFacturaCliente.Pendiente && !f.EstaVencida)
            .Sum(f => f.Monto);

        Indicadores.Clear();
        Indicadores.Add(new Indicador("Total vendido", totalVendido.ToString("N2"), "en el período"));
        Indicadores.Add(new Indicador("Cobrado", cobrado.ToString("N2"), "de facturas emitidas en el período"));
        Indicadores.Add(new Indicador("Pendiente", pendiente.ToString("N2"), "todavía dentro de plazo"));
        Indicadores.Add(new Indicador("Vencido", vencido.ToString("N2"), "fuera de plazo",
            vencido > 0 ? EstadoIndicador.Critico : EstadoIndicador.Normal));
    }

    /// <summary>
    /// Exporta SOLO la vista que está abierta en pantalla (Por producto / Por cliente / Detalle),
    /// no las tres a la vez: el archivo tiene que coincidir con lo que la persona está mirando
    /// cuando aprieta el botón, no siempre lo primero.
    /// </summary>
    private void ExportarExcel()
    {
        string nombreVista;
        IReadOnlyList<string> encabezados;
        IReadOnlyList<IReadOnlyList<string>> filas;

        switch (VistaActual)
        {
            case VistaPorCliente:
                nombreVista = "Por cliente";
                encabezados = ["Cliente", "Despachos", "Monto"];
                filas = PorCliente.Select(f => (IReadOnlyList<string>)
                    [f.ClienteNombre, f.CantidadDespachos.ToString(), f.MontoTexto]).ToList();
                break;

            case VistaDetalle:
                nombreVista = "Detalle";
                encabezados = ["Fecha", "Despacho", "Cliente", "Producto", "Cantidad", "Precio unitario", "Subtotal", "Proceso de origen"];
                filas = Detalle.Select(f => (IReadOnlyList<string>)
                    [f.FechaTexto, f.DespachoNumero, f.ClienteNombre, f.ProductoNombre, f.CantidadTexto,
                     f.PrecioUnitarioTexto, f.SubtotalTexto, f.ProcesoOrigenTexto]).ToList();
                break;

            default:
                nombreVista = "Por producto";
                encabezados = ["Producto", "Cantidad", "Monto"];
                filas = PorProducto.Select(f => (IReadOnlyList<string>)
                    [f.ProductoNombre, f.CantidadTexto, f.MontoTexto]).ToList();
                break;
        }

        var ruta = _dialogos.GuardarArchivo("Exportar reporte de ventas",
            $"ReporteVentas_{nombreVista.Replace(" ", "")}.xlsx", "Libro de Excel (*.xlsx)|*.xlsx");

        if (ruta is null)
            return;

        ExportadorExcel.Exportar(ruta,
        [
            new HojaExcel(nombreVista, encabezados, filas,
                Titulo: $"{Submodulo?.Nombre ?? "Reporte de Ventas"} · {nombreVista}",
                Periodo: $"Período: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}")
        ]);

        _dialogos.Informar("Reporte exportado", $"El archivo se guardó en:\n{ruta}");
    }
}

/// <summary>Fila del desglose "Ventas por producto".</summary>
public sealed record FilaVentaPorProducto(string ProductoNombre, string UnidadMedida, decimal Cantidad, decimal Monto)
{
    public string CantidadTexto => $"{Cantidad:N2} {UnidadMedida}".Trim();
    public string MontoTexto => Monto.ToString("N2");
}

/// <summary>Fila del desglose "Ventas por cliente".</summary>
public sealed record FilaVentaPorCliente(string ClienteNombre, int CantidadDespachos, decimal Monto)
{
    public string MontoTexto => Monto.ToString("N2");
}

/// <summary>
/// Fila de la vista "Detalle": una línea de despacho tal cual, sin agrupar — cliente, producto,
/// cantidad con su unidad, y de qué proceso salió (si se indicó). Es la respuesta directa a "no
/// se puede rastrear el origen de una venta".
/// </summary>
public sealed record FilaVentaDetalle(
    DateTime Fecha,
    string DespachoNumero,
    string ClienteNombre,
    string ProductoNombre,
    string CantidadTexto,
    decimal PrecioUnitario,
    decimal Subtotal,
    string ProcesoOrigenTexto)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string SubtotalTexto => Subtotal.ToString("N2");
}
