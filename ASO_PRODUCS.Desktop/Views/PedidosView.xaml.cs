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

    private void TablaPedidos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: PedidosViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
