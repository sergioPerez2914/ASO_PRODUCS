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
/// Reportes · Cartera: facturas pendientes de Cuentas por Cobrar y por Pagar por período.
///
/// De solo lectura, mismo criterio que <see cref="ReporteGastosViewModel"/>: se arma directo con
/// <see cref="FacturaCliente"/>/<see cref="FacturaProveedor"/> pendientes, sin instanciar
/// <see cref="CuentasPorCobrarService"/>/<see cref="CuentasPorPagarService"/> (que exigen
/// <see cref="ISesionActual"/> por constructor para sus transiciones, que aquí no hacen falta:
/// esto no escribe nada). Una fila por factura, no un total por tercero — mismo criterio que
/// <see cref="ReporteGastosViewModel"/>/<see cref="ReporteVentasViewModel"/>: agrupar por cliente
/// o proveedor escondía el documento, la fecha de emisión y el vencimiento de cada factura
/// puntual. "Vencida"/"Días de atraso" ya son condición derivada en el modelo
/// (<see cref="FacturaCliente.EstaVencida"/>/<see cref="FacturaCliente.PlazoTexto"/>).
/// </summary>
public sealed class ReporteCarteraViewModel : PantallaViewModelBase
{
    public const string VistaPorCliente = "PorCliente";
    public const string VistaPorProveedor = "PorProveedor";

    private readonly IFacturaClienteDataSource _facturasCliente;
    private readonly IFacturaProveedorDataSource _facturasProveedor;
    private readonly IServicioDialogo _dialogos;

    public ReporteCarteraViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearFacturasCliente(), DataSourceFactory.CrearFacturasProveedor(),
               new ServicioDialogo())
    {
    }

    private ReporteCarteraViewModel(Modulo modulo,
                                    Submodulo submodulo,
                                    IFacturaClienteDataSource facturasCliente,
                                    IFacturaProveedorDataSource facturasProveedor,
                                    IServicioDialogo dialogos)
        : base(modulo, submodulo)
    {
        _facturasCliente = facturasCliente;
        _facturasProveedor = facturasProveedor;
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
    public ObservableCollection<FilaCarteraDetalle> PorCliente { get; } = [];
    public ObservableCollection<FilaCarteraDetalle> PorProveedor { get; } = [];

    private string _vistaActual = VistaPorCliente;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarPorCliente => VistaActual == VistaPorCliente;
    public bool MostrarPorProveedor => VistaActual == VistaPorProveedor;

    public bool EsPorCliente
    {
        get => MostrarPorCliente;
        set { if (value) VistaActual = VistaPorCliente; }
    }

    public bool EsPorProveedor
    {
        get => MostrarPorProveedor;
        set { if (value) VistaActual = VistaPorProveedor; }
    }

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var desde = FechaDesde.Date;
        var hasta = FechaHasta.Date;

        var cxcEnRango = _facturasCliente.GetAll()
            .Where(f => f.Estado == EstadoFacturaCliente.Pendiente
                        && f.FechaEmision.Date >= desde && f.FechaEmision.Date <= hasta)
            .ToList();

        // Ordenada por vencimiento: lo más urgente (ya vencido o por vencer antes) primero; sin
        // vencimiento definido queda al final.
        var porCliente = cxcEnRango
            .OrderBy(f => f.FechaVencimiento ?? DateTime.MaxValue)
            .Select(f => new FilaCarteraDetalle(f.NumeroDocumento, f.ClienteNombre, f.FechaEmision,
                f.VencimientoTexto, f.PlazoTexto, f.Monto))
            .ToList();

        PorCliente.Clear();
        foreach (var fila in porCliente)
            PorCliente.Add(fila);

        var cxpEnRango = _facturasProveedor.GetAll()
            .Where(f => f.Estado == EstadoFacturaProveedor.Pendiente
                        && f.FechaEmision.Date >= desde && f.FechaEmision.Date <= hasta)
            .ToList();

        var porProveedor = cxpEnRango
            .OrderBy(f => f.FechaVencimiento ?? DateTime.MaxValue)
            .Select(f => new FilaCarteraDetalle(f.NumeroDocumento, f.ProveedorNombre, f.FechaEmision,
                f.VencimientoTexto, f.PlazoTexto, f.Monto))
            .ToList();

        PorProveedor.Clear();
        foreach (var fila in porProveedor)
            PorProveedor.Add(fila);

        var totalPorCobrar = cxcEnRango.Sum(f => f.Monto);
        var vencidoCliente = cxcEnRango.Where(f => f.EstaVencida).Sum(f => f.Monto);
        var totalPorPagar = cxpEnRango.Sum(f => f.Monto);
        var vencidoProveedor = cxpEnRango.Where(f => f.EstaVencida).Sum(f => f.Monto);

        Indicadores.Clear();
        Indicadores.Add(new Indicador("Por cobrar", totalPorCobrar.ToString("N2"), "facturas pendientes de clientes"));
        Indicadores.Add(new Indicador("Vencido (clientes)", vencidoCliente.ToString("N2"), "fuera de plazo",
            vencidoCliente > 0 ? EstadoIndicador.Critico : EstadoIndicador.Normal));
        Indicadores.Add(new Indicador("Por pagar", totalPorPagar.ToString("N2"), "facturas pendientes de proveedores"));
        Indicadores.Add(new Indicador("Vencido (proveedores)", vencidoProveedor.ToString("N2"), "fuera de plazo",
            vencidoProveedor > 0 ? EstadoIndicador.Critico : EstadoIndicador.Normal));
    }

    /// <summary>Exporta SOLO la vista abierta en pantalla — mismo criterio que
    /// <see cref="ReporteVentasViewModel.ExportarExcel"/>.</summary>
    private void ExportarExcel()
    {
        string nombreVista;
        string encabezadoNombre;
        ObservableCollection<FilaCarteraDetalle> filasOrigen;

        if (VistaActual == VistaPorProveedor)
        {
            nombreVista = "Cuentas por pagar";
            encabezadoNombre = "Proveedor";
            filasOrigen = PorProveedor;
        }
        else
        {
            nombreVista = "Cuentas por cobrar";
            encabezadoNombre = "Cliente";
            filasOrigen = PorCliente;
        }

        string[] encabezados = ["Documento", encabezadoNombre, "Fecha emisión", "Vencimiento", "Estado", "Monto"];
        var filas = filasOrigen.Select(f => (IReadOnlyList<string>)
            [f.Documento, f.Nombre, f.FechaEmisionTexto, f.VencimientoTexto, f.PlazoTexto, f.MontoTexto]).ToList();

        var ruta = _dialogos.GuardarArchivo("Exportar reporte de cartera",
            $"ReporteCartera_{nombreVista.Replace(" ", "")}.xlsx", "Libro de Excel (*.xlsx)|*.xlsx");

        if (ruta is null)
            return;

        ExportadorExcel.Exportar(ruta,
        [
            new HojaExcel(nombreVista, encabezados, filas,
                Titulo: $"{Submodulo?.Nombre ?? "Reporte de Cartera"} · {nombreVista}",
                Periodo: $"Período: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}")
        ]);

        _dialogos.Informar("Reporte exportado", $"El archivo se guardó en:\n{ruta}");
    }
}

/// <summary>Fila de una factura pendiente (cliente o proveedor según la vista), sin agrupar: el
/// Documento y las fechas identifican exactamente cuál es, en vez de un total por tercero.</summary>
public sealed record FilaCarteraDetalle(
    string Documento,
    string Nombre,
    DateTime FechaEmision,
    string VencimientoTexto,
    string PlazoTexto,
    decimal Monto)
{
    public string FechaEmisionTexto => FechaEmision.ToString("dd/MM/yyyy");
    public string MontoTexto => Monto.ToString("N2");
}
