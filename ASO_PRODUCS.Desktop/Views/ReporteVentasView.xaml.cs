using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class ReporteVentasView : UserControl
{
    public ReporteVentasView()
    {
        InitializeComponent();
    }

    /// <summary>Sin conmutador en esta pantalla: el DataContext del DataGrid es la propia
    /// <see cref="ReporteVentasViewModel"/>.</summary>
    private void TablaDetalle_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: ReporteVentasViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
