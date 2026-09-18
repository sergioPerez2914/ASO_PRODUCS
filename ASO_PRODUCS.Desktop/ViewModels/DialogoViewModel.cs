namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Lo que muestra un diálogo de confirmación o de aviso: un título, un cuerpo y uno o dos
/// botones.
///
/// Existe porque <c>ServicioDialogo</c> resolvía las dos cosas con <c>MessageBox</c> del sistema,
/// que es la única pieza de la aplicación que no pasa por el sistema visual: no cambia con el
/// tema oscuro, no crece con la escala de interfaz, no usa la tipografía de la app y trae el
/// icono y los botones de Windows en vez de los de acá. Un cuadro blanco con "Sí/No" en medio de
/// una pantalla oscura se lee como un error del programa.
///
/// No tiene lógica: es el molde de <c>Views/DialogoWindow.xaml</c>. Quien decide qué pregunta y
/// qué hacer con la respuesta sigue siendo el ViewModel que llamó a <c>IServicioDialogo</c>.
/// </summary>
public sealed class DialogoViewModel : ViewModelBase
{
    public DialogoViewModel(string titulo,
                            string mensaje,
                            string textoAceptar,
                            string? textoCancelar = null,
                            bool destructivo = false)
    {
        Titulo = titulo;
        Mensaje = mensaje;
        TextoAceptar = textoAceptar;
        TextoCancelar = textoCancelar ?? "Cancelar";
        MuestraCancelar = textoCancelar is not null || destructivo;
        EsDestructivo = destructivo;
    }

    public string Titulo { get; }
    public string Mensaje { get; }
    public string TextoAceptar { get; }
    public string TextoCancelar { get; }

    /// <summary>Un aviso sin decisión no lleva Cancelar: solo se cierra.</summary>
    public bool MuestraCancelar { get; }

    /// <summary>Pinta el botón de aceptar en rojo. Lo usan borrar y anular.</summary>
    public bool EsDestructivo { get; }

    /// <summary>
    /// Qué botón responde al Enter.
    ///
    /// En lo destructivo es Cancelar, y ese es el motivo principal de este diálogo: el
    /// <c>MessageBox</c> de antes se abría con <c>MessageBoxButton.YesNo</c> y sin
    /// <c>defaultResult</c>, así que el predeterminado era "Sí" y un Enter de inercia —el que
    /// acaba de cerrar el editor— borraba la fila sin que nadie leyera la pregunta.
    /// </summary>
    public bool AceptarPorDefecto => !EsDestructivo;

    public bool CancelarPorDefecto => EsDestructivo;
}
