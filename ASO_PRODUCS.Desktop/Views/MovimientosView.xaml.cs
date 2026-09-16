using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class MovimientosView : UserControl
{
    public MovimientosView()
    {
        InitializeComponent();
    }

    /// <summary>El DataContext del DataGrid es el sub-VM de la lista (ver el "Grid
    /// DataContext={Binding Movimientos}" que lo envuelve en el XAML), no el de la pantalla completa.</summary>
    private void TablaMovimientos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: MovimientosBancoCrudViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
