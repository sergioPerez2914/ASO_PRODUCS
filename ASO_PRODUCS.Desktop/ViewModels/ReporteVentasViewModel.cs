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
/// <see cref="FacturaCliente"/>, sin ningún dato ni migración nueva. Una sola tabla (detalle por
/// línea de despacho, sin agrupar): no hace falta pestaña, mismo criterio que
/// <see cref="ReporteGastosViewModel"/> para su vista "Por categoría".
/// </summary>
public sealed class ReporteVentasViewModel : PantallaViewModelBase
{
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
        VerDetalleCommand = new RelayCommand(VerDetalle);

        Recalcular();
    }

    public ICommand ExportarExcelCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla (ver <c>ReporteVentasView.xaml.cs</c>),
    /// nunca un botón. Abre el despacho real detrás de la línea, mismo criterio que
    /// <see cref="DespachosCrudViewModel.VerDetalleCommand"/>.</summary>
    public ICommand VerDetalleCommand { get; }

    private FilaVentaDetalle? _seleccionada;
    public FilaVentaDetalle? Seleccionada
    {
        get => _seleccionada;
        set => SetProperty(ref _seleccionada, value);
    }

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
    public ObservableCollection<FilaVentaDetalle> Detalle { get; } = [];

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

        // Sin agrupar: una fila por cada línea de despacho, con su cliente y su unidad — es lo
        // que hace falta para rastrear una venta exacta, en vez de un total agregado.
        var detalle = despachosEnRango
            .SelectMany(d => d.Lineas.Select(l => new FilaVentaDetalle(
                d.Fecha, d.Numero, d.ClienteNombre, l.ProductoNombre,
                $"{l.Cantidad:N2} {l.UnidadMedidaSnapshot}".Trim(),
                l.PrecioUnitario, l.Subtotal, d)))
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

    private void ExportarExcel()
    {
        var ruta = _dialogos.GuardarArchivo("Exportar reporte de ventas", "ReporteVentas.xlsx",
            "Libro de Excel (*.xlsx)|*.xlsx");

        if (ruta is null)
            return;

        ExportadorExcel.Exportar(ruta,
        [
            new HojaExcel("Ventas",
                ["Fecha", "Despacho", "Cliente", "Producto", "Cantidad", "Precio unitario", "Subtotal"],
                Detalle.Select(f => (IReadOnlyList<string>)
                    [f.FechaTexto, f.DespachoNumero, f.ClienteNombre, f.ProductoNombre, f.CantidadTexto,
                     f.PrecioUnitarioTexto, f.SubtotalTexto]).ToList(),
                Titulo: Submodulo?.Nombre ?? "Reporte de Ventas",
                Periodo: $"Período: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}")
        ]);

        _dialogos.Informar("Reporte exportado", $"El archivo se guardó en:\n{ruta}");
    }

    private void VerDetalle()
    {
        if (Seleccionada is { } fila)
            _dialogos.MostrarEditor(new DespachoDetalleViewModel(fila.Despacho));
    }
}

/// <summary>
/// Fila de la tabla de Reportes · Ventas: una línea de despacho tal cual, sin agrupar — cliente,
/// producto y cantidad con su unidad. Es la respuesta directa a "no se puede rastrear el origen
/// de una venta".
/// </summary>
public sealed record FilaVentaDetalle(
    DateTime Fecha,
    string DespachoNumero,
    string ClienteNombre,
    string ProductoNombre,
    string CantidadTexto,
    decimal PrecioUnitario,
    decimal Subtotal,
    Despacho Despacho)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string PrecioUnitarioTexto => PrecioUnitario.ToString("N2");
    public string SubtotalTexto => Subtotal.ToString("N2");
}
