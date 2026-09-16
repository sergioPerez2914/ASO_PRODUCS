using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class ReporteCarteraView : UserControl
{
    public ReporteCarteraView()
    {
        InitializeComponent();
    }

    /// <summary>Sin conmutador en esta pantalla: el DataContext de las dos grillas es la propia
    /// <see cref="ReporteCarteraViewModel"/>. Dos manejadores porque cada grilla abre un tipo de
    /// factura distinto.</summary>
    private void TablaPorCliente_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: ReporteCarteraViewModel vm })
            vm.VerDetalleClienteCommand.Execute(null);
    }

    private void TablaPorProveedor_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: ReporteCarteraViewModel vm })
            vm.VerDetalleProveedorCommand.Execute(null);
    }
}
