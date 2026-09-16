using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class CuentasPorPagarView : UserControl
{
    public CuentasPorPagarView()
    {
        InitializeComponent();
    }

    /// <summary>El DataContext del DataGrid es el sub-VM de la lista (ver el "Grid
    /// DataContext={Binding Facturas}" que lo envuelve en el XAML), no el de la pantalla completa.</summary>
    private void Tabla1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: FacturasProveedorCrudViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
