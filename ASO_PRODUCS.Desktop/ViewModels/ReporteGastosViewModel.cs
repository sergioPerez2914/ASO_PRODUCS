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
/// Reportes · Gastos: salidas de banco por período y por categoría.
///
/// No existe una entidad "Gasto" separada, así que se arma con las salidas de
/// <see cref="MovimientoBanco"/>. Cuenta como gasto operativo TODO lo que sale del banco EXCEPTO
/// <see cref="CategoriaMovimiento.Retiro"/> y <see cref="CategoriaMovimiento.AporteCapital"/>
/// (movimiento de capital del dueño, no gasto del negocio) y las transferencias entre cuentas
/// propias (<see cref="OrigenMovimiento.Transferencia"/>, que no es gasto: es mover dinero de un
/// bolsillo a otro). De solo lectura, mismo criterio que <see cref="ReporteProcesosViewModel"/>.
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
    public ObservableCollection<FilaGastoPorCategoria> PorCategoria { get; } = [];

    public override void Recargar() => Recalcular();

    private void Recalcular()
    {
        var desde = FechaDesde.Date;
        var hasta = FechaHasta.Date;

        var gastosEnRango = _movimientos.GetAll()
            .Where(EsGastoOperativo)
            .Where(m => m.Fecha.Date >= desde && m.Fecha.Date <= hasta)
            .ToList();

        var totalGastado = gastosEnRango.Sum(m => m.Monto);

        var porCategoria = gastosEnRango
            .GroupBy(m => m.Categoria)
            .Select(g => new FilaGastoPorCategoria(
                g.First().CategoriaTexto,
                g.Sum(m => m.Monto),
                totalGastado > 0 ? g.Sum(m => m.Monto) / totalGastado * 100 : 0))
            .OrderByDescending(f => f.Monto)
            .ToList();

        PorCategoria.Clear();
        foreach (var fila in porCategoria)
            PorCategoria.Add(fila);

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
            new HojaExcel("Por categoría",
                ["Categoría", "Monto", "% del total"],
                PorCategoria.Select(f => (IReadOnlyList<string>)
                    [f.Categoria, f.MontoTexto, f.PorcentajeTexto]).ToList())
        ]);

        _dialogos.Informar("Reporte exportado", $"El archivo se guardó en:\n{ruta}");
    }
}

/// <summary>Fila del desglose "Gasto por categoría".</summary>
public sealed record FilaGastoPorCategoria(string Categoria, decimal Monto, decimal Porcentaje)
{
    public string MontoTexto => Monto.ToString("N2");
    public string PorcentajeTexto => $"{Porcentaje:N1}%";
}
