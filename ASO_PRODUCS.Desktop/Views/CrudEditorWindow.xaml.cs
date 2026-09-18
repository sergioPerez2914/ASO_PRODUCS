using System.Windows;
using System.Windows.Input;

namespace ASO_PRODUCS.Desktop.Views;

public partial class CrudEditorWindow : Window
{
    public CrudEditorWindow()
    {
        InitializeComponent();

        Loaded += DarFocoAlPrimerCampo;
    }

    /// <summary>
    /// Pone el cursor en el primer campo del formulario al abrir.
    ///
    /// Sin esto el foco se queda en la ventana y hay que pulsar Tab —o hacer clic— antes de poder
    /// escribir la primera letra, en los veintitrés editores. Login y Primer arranque sí lo
    /// hacían, cada uno con su <c>.Focus()</c> en el code-behind; aquí se resuelve una sola vez
    /// para todos, porque el formulario concreto lo resuelve un <c>DataTemplate</c>
    /// (<c>Styles/EditorTemplates.xaml</c>) y ninguno de esos XAML tiene un sitio natural donde
    /// declarar <c>FocusManager.FocusedElement</c>.
    ///
    /// Va en <c>Loaded</c> y no en el constructor porque el árbol de la plantilla todavía no
    /// existe hasta que la ventana se carga: antes de eso no hay a quién dar el foco.
    ///
    /// <c>MoveFocus</c> hacia adelante desde la propia ventana respeta el orden de tabulación y
    /// se salta lo deshabilitado y lo no enfocable, así que un editor que empiece con una
    /// etiqueta, un chip o un campo de solo lectura cae igual en el primer control que acepta
    /// escritura. Las fichas de detalle, que no tienen ninguno, simplemente dejan el foco en el
    /// botón de cerrar.
    /// </summary>
    private void DarFocoAlPrimerCampo(object sender, RoutedEventArgs e)
        => MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
}
