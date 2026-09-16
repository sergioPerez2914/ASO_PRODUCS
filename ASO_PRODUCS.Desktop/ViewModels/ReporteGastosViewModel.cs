using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Reportes · Gastos: salidas de banco por período, sin agrupar.
///
/// No existe una entidad "Gasto" separada, así que se arma con las salidas de
/// <see cref="MovimientoBanco"/>. Cuenta como gasto operativo TODO lo que sale del banco EXCEPTO
/// <see cref="CategoriaMovimiento.Retiro"/> y <see cref="CategoriaMovimiento.AporteCapital"/>
/// (movimiento de capital del dueño, no gasto del negocio) y las transferencias entre cuentas
/// propias (<see cref="OrigenMovimiento.Transferencia"/>, que no es gasto: es mover dinero de un
/// bolsillo a otro). De solo lectura, mismo criterio que <see cref="ReporteProcesosViewModel"/>.
/// Una sola tabla, sin pestaña, mismo criterio que <see cref="ReporteVentasViewModel"/>: una fila
/// por movimiento, no un total por categoría, para no perder A QUIÉN se le pagó — el
/// <see cref="MovimientoBanco.Concepto"/> ya trae el nombre del proveedor cuando el pago vino de
/// una <see cref="FacturaProveedor"/> (ver <see cref="MovimientosService.RegistrarPagoProveedor"/>),
/// y para un gasto manual es la descripción que cargó quien lo registró.
/// </summary>
public sealed class ReporteGastosViewModel : PantallaViewModelBase
{
    private readonly IMovimientoBancoDataSource _movimientos;
    private readonly IServicioDialogo _dialogos;

    public ReporteGastosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearMovimientosBanco(), new ServicioDialogo())
    {
    }

    private ReporteGastosViewModel(Modulo modulo,
                                   Submodulo submodulo,
                                   IMovimientoBancoDataSource movimientos,
                                   IServicioDialogo dialogos)
        : base(modulo, submodulo)
    {
        _movimientos = movimientos;
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
    public ObservableCollection<FilaGasto> Gastos { get; } = [];

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var desde = FechaDesde.Date;
        var hasta = FechaHasta.Date;

        var gastosEnRango = _movimientos.GetAll()
            .Where(EsGastoOperativo)
            .Where(m => m.Fecha.Date >= desde && m.Fecha.Date <= hasta)
            .OrderByDescending(m => m.Fecha)
            .ToList();

        var totalGastado = gastosEnRango.Sum(m => m.Monto);

        Gastos.Clear();
        foreach (var m in gastosEnRango)
            Gastos.Add(new FilaGasto(m.Fecha, m.CategoriaTexto, m.Concepto, m.Monto));

        Indicadores.Clear();
        Indicadores.Add(new Indicador("Total gastado", totalGastado.ToString("N2"), "en el período"));
    }

    /// <summary>Salida de banco registrada/conciliada, sin contar retiros, aportes de capital ni
    /// transferencias entre cuentas propias — ver el comentario de cabecera de la clase.</summary>
    private static bool EsGastoOperativo(MovimientoBanco m) =>
        m.Tipo == TipoMovimientoBanco.Salida
        && m.Estado != EstadoMovimientoBanco.Anulado
        && m.Categoria is not (CategoriaMovimiento.Retiro or CategoriaMovimiento.AporteCapital)
        && m.Origen != OrigenMovimiento.Transferencia;

    private void ExportarExcel()
    {
        var ruta = _dialogos.GuardarArchivo("Exportar reporte de gastos", "ReporteGastos.xlsx",
            "Libro de Excel (*.xlsx)|*.xlsx");

        if (ruta is null)
            return;

        ExportadorExcel.Exportar(ruta,
        [
            new HojaExcel("Gastos",
                ["Fecha", "Categoría", "Concepto", "Monto"],
                Gastos.Select(f => (IReadOnlyList<string>)
                    [f.FechaTexto, f.Categoria, f.Concepto, f.MontoTexto]).ToList(),
                Titulo: Submodulo?.Nombre ?? "Reporte de Gastos",
                Periodo: $"Período: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}")
        ]);

        _dialogos.Informar("Reporte exportado", $"El archivo se guardó en:\n{ruta}");
    }
}

/// <summary>Fila de un gasto (salida de banco) tal cual, sin agrupar — el Concepto es lo que
/// identifica a quién se le pagó o para qué, ver el comentario de cabecera de la clase.</summary>
public sealed record FilaGasto(DateTime Fecha, string Categoria, string Concepto, decimal Monto)
{
    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string MontoTexto => Monto.ToString("N2");
}
