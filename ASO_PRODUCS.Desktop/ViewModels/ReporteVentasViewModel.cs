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
/// <see cref="FacturaCliente"/>, sin ningún dato ni migración nueva. Una sola tabla, sin pestaña,
/// mismo criterio que <see cref="ReporteGastosViewModel"/> para su vista "Por categoría".
///
/// La grilla en pantalla respeta el filtro <see cref="SelectedProducto"/>: en "Todos" es una fila
/// por despacho, no por línea (el desglose se ve con doble clic, ver
/// <see cref="VerDetalleCommand"/>); con un producto puntual, una fila por línea de ese producto.
/// <see cref="ExportarExcel"/> en cambio ignora ese filtro a propósito: el Excel siempre sale en
/// detalle completo, una fila por línea de cada despacho, porque ahí no hay doble clic que
/// desarme un "Varios (N productos)".
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

    private string _productoSeleccionado = "Todos";

    /// <summary>"Todos" mantiene la fila consolidada por despacho (con "Varios" cuando
    /// corresponde); elegir un producto puntual cambia a una fila por línea de ese producto —
    /// la vista que hace falta para exportar a Excel sin ambigüedad, ver
    /// <see cref="ExportarExcel"/>.</summary>
    public string SelectedProducto
    {
        get => _productoSeleccionado;
        set { if (SetProperty(ref _productoSeleccionado, value)) Recalcular(); }
    }

    public ObservableCollection<string> Productos { get; } = [];
    public ObservableCollection<Indicador> Indicadores { get; } = [];
    public ObservableCollection<FilaVentaDetalle> Detalle { get; } = [];

    // Los despachos del rango vigente, para que ExportarExcel arme su propio detalle sin
    // depender del filtro de Producto (ver ExportarExcel).
    private List<Despacho> _despachosEnRango = [];

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var desde = FechaDesde.Date;
        var hasta = FechaHasta.Date;

        var despachosEnRango = _despachos.GetAll()
            .Where(d => d.Tipo == TipoDespacho.Venta && d.Estado == EstadoDespacho.Registrado
                        && d.Fecha.Date >= desde && d.Fecha.Date <= hasta)
            .ToList();

        _despachosEnRango = despachosEnRango;

        var totalVendido = despachosEnRango.Sum(d => d.Total);

        // Opciones del filtro de producto: solo los que aparecen en el rango visible, no un
        // catálogo aparte. Si el producto elegido deja de estar (cambió el rango), cae a "Todos".
        // Ojo: reconstruir Productos con Clear()+Add() cuando el contenido no cambió resetea a
        // null el SelectedItem del ComboBox (WPF), lo que dispara este mismo setter de nuevo y
        // pisa la selección que se acababa de elegir — por eso solo se toca la colección cuando
        // el conjunto de nombres realmente cambió (típicamente al mover el rango de fechas).
        var productosDeseados = new List<string> { "Todos" };
        productosDeseados.AddRange(despachosEnRango
            .SelectMany(d => d.Lineas.Select(l => l.ProductoNombre))
            .Distinct()
            .OrderBy(n => n));

        if (!Productos.SequenceEqual(productosDeseados))
        {
            Productos.Clear();
            foreach (var nombre in productosDeseados)
                Productos.Add(nombre);
        }

        if (!Productos.Contains(_productoSeleccionado))
        {
            _productoSeleccionado = "Todos";
            OnPropertyChanged(nameof(SelectedProducto));
        }

        // Con "Todos": una fila por despacho, no por línea (repetir cliente/fecha por cada
        // producto se veía repetitivo y el desglose ya está a un doble clic, VerDetalle). Con una
        // sola línea se sigue mostrando el producto/cantidad/precio real; con varias, "Varios (N
        // productos)" y cantidad/precio vacíos porque mezclar unidades o precios distintos en una
        // sola cifra no dice nada. Con un producto puntual elegido, en cambio, hace falta el
        // desglose real para exportar a Excel (ahí no hay doble clic): una fila por línea de ese
        // producto, igual que antes de consolidar por despacho.
        IEnumerable<FilaVentaDetalle> detalle = _productoSeleccionado == "Todos"
            ? despachosEnRango
                .Select(d => new FilaVentaDetalle(
                    d.Fecha, d.Numero, d.ClienteNombre,
                    d.Lineas.Count == 1 ? d.Lineas[0].ProductoNombre : $"Varios ({d.Lineas.Count} productos)",
                    d.Lineas.Count == 1 ? $"{d.Lineas[0].Cantidad:N2} {d.Lineas[0].UnidadMedidaSnapshot}".Trim() : string.Empty,
                    d.Lineas.Count == 1 ? d.Lineas[0].PrecioUnitario.ToString("N2") : string.Empty,
                    d.Total, d))
            : despachosEnRango
                .SelectMany(d => d.Lineas
                    .Where(l => l.ProductoNombre == _productoSeleccionado)
                    .Select(l => new FilaVentaDetalle(
                        d.Fecha, d.Numero, d.ClienteNombre, l.ProductoNombre,
                        $"{l.Cantidad:N2} {l.UnidadMedidaSnapshot}".Trim(),
                        l.PrecioUnitario.ToString("N2"),
                        l.Subtotal, d)));

        var detalleOrdenado = detalle.OrderByDescending(f => f.Fecha).ToList();

        Detalle.Clear();
        foreach (var fila in detalleOrdenado)
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

        // El Excel siempre sale en detalle, una fila por línea (igual que antes de consolidar la
        // grilla por despacho): en Excel no hay doble clic para abrir un "Varios (N productos)",
        // así que no depende del filtro de Producto de la pantalla — ese filtro es solo para
        // mirar en pantalla, ver SelectedProducto.
        var filasExport = _despachosEnRango
            .SelectMany(d => d.Lineas.Select(l => new FilaVentaDetalle(
                d.Fecha, d.Numero, d.ClienteNombre, l.ProductoNombre,
                $"{l.Cantidad:N2} {l.UnidadMedidaSnapshot}".Trim(),
                l.PrecioUnitario.ToString("N2"),
                l.Subtotal, d)))
            .OrderByDescending(f => f.Fecha)
            .ToList();

        ExportadorExcel.Exportar(ruta,
        [
            new HojaExcel("Ventas",
                ["Fecha", "Despacho", "Cliente", "Producto", "Cantidad", "Precio unitario", "Total"],
                filasExport.Select(f => (IReadOnlyList<string>)
                    [f.FechaTexto, f.DespachoNumero, f.ClienteNombre, f.ProductoNombre, f.CantidadTexto,
                     f.PrecioUnitarioTexto, f.TotalTexto]).ToList(),
                Titulo: Submodulo?.Nombre ?? "Reporte de Ventas",
                Periodo: $"Período: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}")
        ]);

        Aviso.Mostrar($"Reporte exportado a {System.IO.Path.GetFileName(ruta)}");
    }

    private void VerDetalle()
    {
        if (Seleccionada is { } fila)
            _dialogos.MostrarEditor(new DespachoDetalleViewModel(fila.Despacho));
    }
}

/// <summary>
/// Fila de la tabla de Reportes · Ventas. Con el filtro de producto en "Todos" representa un
/// despacho completo (el desglose se ve con doble clic,
/// <see cref="ReporteVentasViewModel.VerDetalleCommand"/>); con un producto elegido, representa
/// una sola línea de ese despacho. <see cref="ProductoNombre"/>/<see cref="CantidadTexto"/>/
/// <see cref="PrecioUnitarioTexto"/>/<see cref="Total"/> ya vienen resueltos desde
/// <c>Recalcular</c> según cuál sea el caso.
/// </summary>
public sealed record FilaVentaDetalle(
    DateTime Fecha,
    string DespachoNumero,
    string ClienteNombre,
    string ProductoNombre,
    string CantidadTexto,
    string PrecioUnitarioTexto,
    decimal Total,
    Despacho Despacho)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string TotalTexto => Total.ToString("N2");
}
