using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class ReporteGastosView : UserControl
{
    public ReporteGastosView()
    {
        InitializeComponent();
    }

    /// <summary>Sin conmutador en esta pantalla: el DataContext del DataGrid es la propia
    /// <see cref="ReporteGastosViewModel"/>.</summary>
    private void TablaGastos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: ReporteGastosViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
