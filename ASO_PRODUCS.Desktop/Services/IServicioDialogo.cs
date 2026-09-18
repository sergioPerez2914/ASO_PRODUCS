using ASO_PRODUCS.Desktop.ViewModels;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Presentación de diálogos modales (editor CRUD, confirmaciones). Mantiene las
/// ViewModels libres de referencias directas a <c>Window</c>/<c>MessageBox</c>.
/// </summary>
public interface IServicioDialogo
{
    /// <returns><c>true</c> si el usuario guardó los cambios.</returns>
    bool MostrarEditor(CrudEditorViewModelBase editor);

    /// <param name="textoAceptar">El verbo del botón que confirma ("Eliminar", "Anular"). Un
    /// botón que dice lo que va a pasar se lee sin releer la pregunta; "Sí" no.</param>
    /// <param name="destructivo">Pinta el botón en rojo y, sobre todo, deja el Enter en Cancelar:
    /// lo irreversible no se confirma de inercia.</param>
    bool Confirmar(string titulo, string mensaje, string? textoAceptar = null, bool destructivo = false);

    /// <summary>Aviso sin decisión: reglas de negocio rechazadas, resultados de una acción.</summary>
    void Informar(string titulo, string mensaje);

    /// <summary>Pide dónde guardar un archivo (exportar a Excel, por ejemplo).</summary>
    /// <param name="filtro">Formato de <see cref="Microsoft.Win32.SaveFileDialog.Filter"/>, p.
    /// ej. <c>"Libro de Excel (*.xlsx)|*.xlsx"</c>.</param>
    /// <returns>La ruta elegida, o <c>null</c> si el usuario canceló.</returns>
    string? GuardarArchivo(string titulo, string nombreSugerido, string filtro);
}
