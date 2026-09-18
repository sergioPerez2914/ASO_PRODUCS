using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ASO_PRODUCS.Desktop.Controls;

/// <summary>
/// "Abrir lo que está seleccionado" en una tabla: doble clic <b>y Enter</b>.
///
/// <para><b>Por qué existe.</b> Catorce listados de documentos abrían su ficha con un
/// <c>MouseDoubleClick</c> escrito a mano en el code-behind, cada uno con el mismo <c>if</c>
/// para sacar el ViewModel del <c>DataContext</c>; los cinco catálogos maestros (Proveedores,
/// Clientes, Almacén, Existencias, Productos) no abrían nada al hacer doble clic, aunque todos
/// tienen un botón Editar. Y en ninguno de los diecinueve funcionaba el teclado: seleccionar una
/// fila con las flechas y pulsar Enter no hacía nada, así que la tabla solo se podía usar con el
/// ratón.</para>
///
/// <para>En XAML: <c>controls:AbrirFila.Comando="{Binding VerDetalleCommand}"</c> sobre el
/// <c>DataGrid</c>. El binding se resuelve contra el <c>DataContext</c> del propio
/// <c>DataGrid</c>, que en las pantallas conmutables es el sub-ViewModel del padrón, igual que
/// hacía el code-behind.</para>
/// </summary>
public static class AbrirFila
{
    public static readonly DependencyProperty ComandoProperty =
        DependencyProperty.RegisterAttached(
            "Comando",
            typeof(ICommand),
            typeof(AbrirFila),
            new PropertyMetadata(null, AlCambiarComando));

    public static void SetComando(DependencyObject destino, ICommand? valor)
        => destino.SetValue(ComandoProperty, valor);

    public static ICommand? GetComando(DependencyObject destino)
        => (ICommand?)destino.GetValue(ComandoProperty);

    private static void AlCambiarComando(DependencyObject destino, DependencyPropertyChangedEventArgs e)
    {
        if (destino is not DataGrid grilla)
            return;

        grilla.MouseDoubleClick -= AlDobleClic;
        grilla.PreviewKeyDown -= AlPulsarTecla;

        if (e.NewValue is not null)
        {
            grilla.MouseDoubleClick += AlDobleClic;

            // Preview: el DataGrid se queda con Enter para mover la selección a la fila de abajo
            // (es su comportamiento de edición, aunque la tabla sea de solo lectura). Si se
            // escuchara el KeyDown normal, el evento llegaría ya marcado como manejado.
            grilla.PreviewKeyDown += AlPulsarTecla;
        }
    }

    private static void AlDobleClic(object remitente, MouseButtonEventArgs e)
    {
        if (remitente is not DataGrid grilla)
            return;

        // Doble clic en la cabecera (ordenar) o en el hueco de debajo de la última fila no es
        // "abrir": sin esto, ordenar por una columna abría la ficha de lo que hubiera
        // seleccionado.
        if (e.OriginalSource is DependencyObject origen && !EstaDentroDeUnaFila(origen))
            return;

        Ejecutar(grilla);
    }

    private static void AlPulsarTecla(object remitente, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || remitente is not DataGrid grilla)
            return;

        if (Ejecutar(grilla))
            e.Handled = true;
    }

    private static bool Ejecutar(DataGrid grilla)
    {
        if (GetComando(grilla) is not { } comando || grilla.SelectedItem is null)
            return false;

        if (!comando.CanExecute(null))
            return false;

        comando.Execute(null);
        return true;
    }

    /// <summary>
    /// Sube por el árbol visual hasta encontrar la fila. Se para en el propio
    /// <see cref="DataGrid"/>: si llega hasta él sin haber pasado por una fila, el clic fue en la
    /// cabecera o en el fondo.
    /// </summary>
    private static bool EstaDentroDeUnaFila(DependencyObject origen)
    {
        // Se recorre solo mientras el nodo sea Visual: GetParent lanza con cualquier otra cosa,
        // y el origen de un clic puede no serlo.
        for (var actual = origen; actual is Visual or System.Windows.Media.Media3D.Visual3D;
             actual = VisualTreeHelper.GetParent(actual))
        {
            if (actual is DataGridRow)
                return true;

            if (actual is DataGrid or DataGridColumnHeader)
                return false;
        }

        return false;
    }
}
