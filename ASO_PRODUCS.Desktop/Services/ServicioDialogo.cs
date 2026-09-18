using System;
using System.Windows;
using ASO_PRODUCS.Desktop.ViewModels;
using ASO_PRODUCS.Desktop.Views;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Implementación por defecto de <see cref="IServicioDialogo"/> usando ventanas WPF.
/// </summary>
public class ServicioDialogo : IServicioDialogo
{
    public bool MostrarEditor(CrudEditorViewModelBase editor)
    {
        // El ancho se fija aquí y no por binding: el DataContext se asigna después de que el
        // XAML se parsea, y un formulario largo (la remesa) necesita más de los 420 por defecto.
        var ventana = new CrudEditorWindow { DataContext = editor, Width = editor.AnchoEditor };

        if (Application.Current?.MainWindow is { } owner && owner != ventana)
            ventana.Owner = owner;

        editor.SolicitarCierre += (_, guardo) => ventana.DialogResult = guardo;

        // La escala de la interfaz no llegaba aqui: el LayoutTransform vivia solo en MainWindow,
        // asi que a 125 % el shell crecia y los formularios se quedaban a 100 %.
        EscalaVentana.Aplicar(ventana);

        // Tope contra el área de trabajo de la pantalla, después de escalar: un editor ancho
        // (960 con AnchoEditor.Amplio) a 150 % de escala pasaría de 1400 px, más ancho que muchas
        // pantallas; y un formulario largo con SizeToContent="Height" y sin este tope crecería sin
        // límite, con ResizeMode="NoResize" no dejaría manera de recuperarlo. Width se recorta
        // directo porque ya está fijado; Height se deja que lo siga decidiendo SizeToContent, así
        // que MaxHeight es lo único que hace falta para que el ScrollViewer de la ventana entre a
        // trabajar en vez de salirse de la pantalla.
        var areaTrabajo = SystemParameters.WorkArea;
        ventana.Width = Math.Min(ventana.Width, areaTrabajo.Width * 0.9);
        ventana.MaxHeight = areaTrabajo.Height * 0.9;

        return ventana.ShowDialog() == true;
    }

    public bool Confirmar(string titulo, string mensaje, string? textoAceptar = null, bool destructivo = false)
        => Mostrar(new DialogoViewModel(titulo, mensaje,
                                        textoAceptar ?? (destructivo ? "Eliminar" : "Aceptar"),
                                        "Cancelar",
                                        destructivo)) == true;

    public void Informar(string titulo, string mensaje)
        => Mostrar(new DialogoViewModel(titulo, mensaje, "Entendido"));

    /// <summary>
    /// Abre <see cref="DialogoWindow"/> con las mismas tres cortesías que ya recibe el editor
    /// CRUD, y por los mismos motivos: dueño (para que se centre sobre el shell y no sobre la
    /// pantalla), escala de interfaz, y tope contra el área de trabajo por si el mensaje es largo
    /// y la escala alta. Sin esto un aviso a 150 % se salía de la pantalla y, con
    /// <c>ResizeMode="NoResize"</c>, no había forma de recuperarlo.
    /// </summary>
    private static bool? Mostrar(DialogoViewModel dialogo)
    {
        var ventana = new DialogoWindow { DataContext = dialogo };

        if (Application.Current?.MainWindow is { } owner && owner != ventana)
            ventana.Owner = owner;

        EscalaVentana.Aplicar(ventana);

        var areaTrabajo = SystemParameters.WorkArea;
        ventana.Width = Math.Min(ventana.Width, areaTrabajo.Width * 0.9);
        ventana.MaxHeight = areaTrabajo.Height * 0.9;

        return ventana.ShowDialog();
    }

    public string? GuardarArchivo(string titulo, string nombreSugerido, string filtro)
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = titulo,
            FileName = nombreSugerido,
            Filter = filtro
        };

        var resultado = Application.Current?.MainWindow is { } owner
            ? dialogo.ShowDialog(owner)
            : dialogo.ShowDialog();

        return resultado == true ? dialogo.FileName : null;
    }
}
