using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class PedidosView : UserControl
{
    public PedidosView()
    {
        InitializeComponent();
    }

    /// <summary>El DataContext del DataGrid es el sub-VM de la pestaña Pedidos (ver el "Grid
    /// DataContext={Binding Pedidos}" que lo envuelve en el XAML), no el de la pantalla completa.</summary>
    private void TablaPedidos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: PedidosCrudViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }

    /// <summary>El DataContext del DataGrid es el sub-VM de la pestaña Despachos (ver el "Grid
    /// DataContext={Binding Despachos}" que lo envuelve en el XAML), no el de la pantalla completa.</summary>
    private void TablaDespachos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: DespachosCrudViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
