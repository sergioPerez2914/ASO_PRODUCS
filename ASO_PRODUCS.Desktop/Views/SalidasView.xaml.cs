using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class SalidasView : UserControl
{
    public SalidasView()
    {
        InitializeComponent();
    }

    /// <summary>Sin conmutador en esta pantalla: el DataContext del DataGrid es la propia
    /// <see cref="SalidasViewModel"/>.</summary>
    private void TablaSalidas_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: SalidasViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
