using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class SalidasMateriaPrimaView : UserControl
{
    public SalidasMateriaPrimaView()
    {
        InitializeComponent();
    }

    /// <summary>Sin conmutador en esta pantalla: el DataContext del DataGrid es la propia
    /// <see cref="SalidasMateriaPrimaViewModel"/>.</summary>
    private void TablaSalidas_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: SalidasMateriaPrimaViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
