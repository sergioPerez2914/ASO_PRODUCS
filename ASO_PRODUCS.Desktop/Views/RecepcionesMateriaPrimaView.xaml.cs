using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class RecepcionesMateriaPrimaView : UserControl
{
    public RecepcionesMateriaPrimaView()
    {
        InitializeComponent();
    }

    /// <summary>Sin conmutador en esta pantalla: el DataContext del DataGrid es la propia
    /// <see cref="RecepcionesMateriaPrimaViewModel"/>.</summary>
    private void TablaRecepciones_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: RecepcionesMateriaPrimaViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
