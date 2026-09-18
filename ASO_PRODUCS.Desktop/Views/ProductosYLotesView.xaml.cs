using System.Windows.Controls;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Views;

public partial class ProductosYLotesView : UserControl
{
    public ProductosYLotesView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Doble clic sobre una tarjeta abre el editor del producto, igual que el doble clic sobre
    /// una fila en el resto de los listados.
    ///
    /// No se usa <c>Controls/AbrirFila.cs</c> porque ese comportamiento adjunto es de
    /// <c>DataGrid</c> —sube por el árbol buscando una <c>DataGridRow</c> para no disparar al
    /// pulsar la cabecera— y aquí la lista es un <c>ListBox</c> de tarjetas. La guarda equivalente
    /// es <c>SelectedItem</c>: un doble clic en el hueco entre tarjetas no selecciona nada, y el
    /// <c>CanExecute</c> de Editar ya exige que haya algo marcado.
    /// </summary>
    private void RejillaProductos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox { DataContext: ProductosCrudViewModel vm, SelectedItem: not null })
            return;

        if (vm.EditarCommand.CanExecute(null))
            vm.EditarCommand.Execute(null);
    }
}
