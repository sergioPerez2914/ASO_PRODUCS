using System;
using System.Windows;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// El "hecho" discreto que aparece un momento abajo a la derecha y se va solo.
///
/// Es estática por el mismo motivo que <see cref="CambiosDeDatos"/>: quien avisa es un ViewModel
/// de pantalla, quien lo pinta es el shell, y no hay constructor común entre los dos donde
/// inyectar nada.
///
/// <para><b>Qué viene a resolver.</b> El aviso ya existía —<c>ViewModels/AvisoGuardado.cs</c>—
/// pero solo dentro de Configuración, donde los ajustes se aplican al instante y hacía falta
/// decir que se habían guardado. En el resto de la aplicación, guardar un proveedor, registrar un
/// despacho o anular un documento no confirmaban nada: la única señal era que la tabla se
/// recargaba sola, y con la fila fuera de pantalla eso no se ve. Los pocos sitios que sí
/// confirmaban lo hacían con un <c>MessageBox</c>, que para dar una buena noticia pide un clic de
/// más.</para>
///
/// <para><b>Solo para lo que salió bien.</b> Un error no va por acá: se queda en
/// <see cref="IServicioDialogo.Informar"/>, que exige acuse. Un aviso que se desvanece es justo
/// lo que no se quiere para algo que hay que leer.</para>
/// </summary>
public static class Aviso
{
    /// <summary>Alguien pidió mostrar un aviso. Se entrega SIEMPRE en el hilo de interfaz.</summary>
    public static event Action<string>? Pedido;

    /// <summary>
    /// Muestra el aviso.
    ///
    /// Se encola en el despachador y no se entrega en el acto, mismo criterio que
    /// <see cref="CambiosDeDatos.Publicar"/>: quien avisa suele estar terminando de guardar, con
    /// un diálogo modal todavía sin cerrar, y el aviso tiene que aparecer sobre el shell y no
    /// detrás de una ventana que está a punto de irse.
    /// </summary>
    public static void Mostrar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return;

        var despachador = Application.Current?.Dispatcher;

        // Sin interfaz (arranque temprano, pruebas) no hay a quién avisar.
        if (despachador is null)
            return;

        despachador.BeginInvoke(new Action(() => Pedido?.Invoke(texto)));
    }
}
