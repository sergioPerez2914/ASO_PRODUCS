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
/// Reportes · Cartera: antigüedad de saldos de Cuentas por Cobrar y por Pagar por período.
///
/// De solo lectura, mismo criterio que <see cref="ReporteGastosViewModel"/>: se arma directo con
/// <see cref="FacturaCliente"/>/<see cref="FacturaProveedor"/> pendientes, sin instanciar
/// <see cref="CuentasPorCobrarService"/>/<see cref="CuentasPorPagarService"/> (que exigen
/// <see cref="ISesionActual"/> por constructor para sus transiciones, que aquí no hacen falta:
/// esto no escribe nada). "Vencida"/"Días de atraso" ya son condición derivada en el modelo
/// (<see cref="FacturaCliente.EstaVencida"/>/<see cref="FacturaCliente.DiasParaVencer"/>), así
/// que la antigüedad se calcula sin tocar el modelo de datos.
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
    public ObservableCollection<FilaCarteraPorTercero> PorCliente { get; } = [];
    public ObservableCollection<FilaCarteraPorTercero> PorProveedor { get; } = [];

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

        var porCliente = cxcEnRango
            .GroupBy(f => f.ClienteNombre)
            .Select(g => ArmarFila(g.Key, g.Select(f => (f.Monto, f.DiasParaVencer))))
            .OrderByDescending(f => f.Total)
            .ToList();

        PorCliente.Clear();
        foreach (var fila in porCliente)
            PorCliente.Add(fila);

        var cxpEnRango = _facturasProveedor.GetAll()
            .Where(f => f.Estado == EstadoFacturaProveedor.Pendiente
                        && f.FechaEmision.Date >= desde && f.FechaEmision.Date <= hasta)
            .ToList();

        var porProveedor = cxpEnRango
            .GroupBy(f => f.ProveedorNombre)
            .Select(g => ArmarFila(g.Key, g.Select(f => (f.Monto, f.DiasParaVencer))))
            .OrderByDescending(f => f.Total)
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

    /// <summary>
    /// Bucketiza el saldo de un tercero por antigüedad. <c>DiasParaVencer</c> es negativo cuando
    /// ya venció (ver <see cref="FacturaCliente.DiasParaVencer"/>), así que el atraso es su valor
    /// cambiado de signo; una factura sin vencer o sin vencimiento definido cae en "Al día".
    /// </summary>
    private static FilaCarteraPorTercero ArmarFila(string nombre, IEnumerable<(decimal Monto, int DiasParaVencer)> facturas)
    {
        decimal alDia = 0, dias1a30 = 0, dias31a60 = 0, dias61a90 = 0, mas90 = 0;

        foreach (var (monto, diasParaVencer) in facturas)
        {
            var atraso = -diasParaVencer;

            if (atraso <= 0) alDia += monto;
            else if (atraso <= 30) dias1a30 += monto;
            else if (atraso <= 60) dias31a60 += monto;
            else if (atraso <= 90) dias61a90 += monto;
            else mas90 += monto;
        }

        return new FilaCarteraPorTercero(nombre, alDia, dias1a30, dias31a60, dias61a90, mas90);
    }

    /// <summary>Exporta SOLO la vista abierta en pantalla — mismo criterio que
    /// <see cref="ReporteVentasViewModel.ExportarExcel"/>.</summary>
    private void ExportarExcel()
    {
        string[] encabezados = ["Cliente/Proveedor", "Al día", "1-30 días", "31-60 días", "61-90 días", "+90 días", "Total"];

        string nombreVista;
        IReadOnlyList<IReadOnlyList<string>> filas;

        if (VistaActual == VistaPorProveedor)
        {
            nombreVista = "Cuentas por pagar";
            filas = PorProveedor.Select(f => (IReadOnlyList<string>)
                [f.Nombre, f.AlDiaTexto, f.Dias1a30Texto, f.Dias31a60Texto, f.Dias61a90Texto, f.Mas90Texto, f.TotalTexto]).ToList();
        }
        else
        {
            nombreVista = "Cuentas por cobrar";
            filas = PorCliente.Select(f => (IReadOnlyList<string>)
                [f.Nombre, f.AlDiaTexto, f.Dias1a30Texto, f.Dias31a60Texto, f.Dias61a90Texto, f.Mas90Texto, f.TotalTexto]).ToList();
        }

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

/// <summary>Fila del desglose de antigüedad de saldos, por cliente o por proveedor según la vista.</summary>
public sealed record FilaCarteraPorTercero(
    string Nombre,
    decimal AlDia,
    decimal Dias1a30,
    decimal Dias31a60,
    decimal Dias61a90,
    decimal Mas90)
{
    public decimal Total => AlDia + Dias1a30 + Dias31a60 + Dias61a90 + Mas90;

    public string AlDiaTexto => AlDia.ToString("N2");
    public string Dias1a30Texto => Dias1a30.ToString("N2");
    public string Dias31a60Texto => Dias31a60.ToString("N2");
    public string Dias61a90Texto => Dias61a90.ToString("N2");
    public string Mas90Texto => Mas90.ToString("N2");
    public string TotalTexto => Total.ToString("N2");
}
