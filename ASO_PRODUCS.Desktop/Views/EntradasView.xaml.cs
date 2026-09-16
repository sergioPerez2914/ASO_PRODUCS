using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class EntradasView : UserControl
{
    public EntradasView()
    {
        InitializeComponent();
    }

    /// <summary>Sin conmutador en esta pantalla: el DataContext del DataGrid es la propia
    /// <see cref="EntradasViewModel"/>, a diferencia de las pantallas con pestañas.</summary>
    private void TablaEntradas_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: EntradasViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
