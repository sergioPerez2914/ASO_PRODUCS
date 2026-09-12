using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class ProcesosProduccionView : UserControl
{
    public ProcesosProduccionView()
    {
        InitializeComponent();
    }

    /// <summary>Solo reenvía al comando; el DataContext del DataGrid es el sub-VM de la lista
    /// (ver el "Grid DataContext={Binding Procesos}" que lo envuelve en el XAML), no el de la
    /// pantalla completa.</summary>
    private void TablaProcesos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid { DataContext: ProcesosProduccionCrudViewModel vm })
            vm.VerDetalleCommand.Execute(null);
    }
}
