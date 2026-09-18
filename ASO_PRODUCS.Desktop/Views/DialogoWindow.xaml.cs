using System.Windows;

namespace ASO_PRODUCS.Desktop.Views;

/// <summary>
/// La ventana de confirmar y de avisar, con el sistema visual de la aplicación en vez del
/// <c>MessageBox</c> de Windows. La arma <c>Services/ServicioDialogo.cs</c>; el molde de lo que
/// muestra es <c>ViewModels/DialogoViewModel.cs</c>.
/// </summary>
public partial class DialogoWindow : Window
{
    public DialogoWindow()
    {
        InitializeComponent();
    }

    // El botón de aceptar cierra con true. Va en el code-behind y no en un comando porque no
    // decide nada: el DialogoViewModel no tiene lógica, y cablear un RelayCommand para asignar
    // DialogResult sería más ceremonia que la que ya tiene IsCancel para el otro botón.
    private void Aceptar_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
