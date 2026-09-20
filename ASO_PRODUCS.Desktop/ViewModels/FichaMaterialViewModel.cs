using System.Windows.Input;
using ASO_PRODUCS.Desktop.Controls;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Ficha de solo lectura de un artículo del almacén o de un tipo de materia prima: su precio
/// promedio, las compras de las que sale y lo que debería quedar de cada una.
///
/// Una sola ficha para los dos módulos: lo que cambia entre Almacén y Existencias es de qué tablas
/// salen los documentos, y eso ya lo resuelve <c>CostosMaterialesService</c> antes de llegar aquí.
///
/// Mismo patrón que las demás fichas (<see cref="EntradaDetalleViewModel"/> y compañía): hereda del
/// editor modal, no valida nada y su único botón dice "Cerrar".
/// </summary>
public sealed class FichaMaterialViewModel : CrudEditorViewModelBase
{
    public FichaMaterialViewModel(FichaMaterial ficha, bool puedeEditar)
    {
        Ficha = ficha;
        PuedeEditar = puedeEditar;

        EditarCommand = new RelayCommand(() =>
        {
            QuiereEditar = true;
            CerrarCommand.Execute(null);
        }, () => PuedeEditar);
    }

    public FichaMaterial Ficha { get; }

    public bool PuedeEditar { get; }

    /// <summary>
    /// El botón "Editar" de la ficha cierra la ventana y deja esto en <c>true</c> para que la
    /// pantalla que la abrió encadene su propio editor. La ficha no puede abrirlo ella misma: no
    /// conoce la entidad, solo la ficha derivada.
    /// </summary>
    public bool QuiereEditar { get; private set; }

    public ICommand EditarCommand { get; }

    /// <summary>El mismo comando del botón "Cerrar" de la ventana, para cerrarla desde dentro.</summary>
    private ICommand CerrarCommand => GuardarCommand;

    public override string Titulo => Ficha.Origen == OrigenMaterial.Articulo
        ? $"Artículo {Ficha.Nombre}"
        : $"Materia prima {Ficha.Nombre}";

    /// <summary>Lleva dos grillas de líneas de hasta seis columnas: el ancho de las demás fichas.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Cerrar";

    public override bool MuestraCancelar => false;

    /// <summary>El indicador se pone en ámbar cuando el número pide atención: sin existencia o por
    /// debajo del mínimo.</summary>
    public EstadoIndicador EstadoExistencia => Ficha.Existencia <= 0
        ? EstadoIndicador.Critico
        : Ficha.BajoMinimo
            ? EstadoIndicador.Atencion
            : EstadoIndicador.Normal;

    /// <summary>Sin una sola compra con precio no hay promedio que mostrar, y el costo de cualquier
    /// proceso que consuma este material sale incompleto — el mismo aviso que da la ficha de un
    /// proceso, visto desde el material.</summary>
    public EstadoIndicador EstadoPrecio => Ficha.SinPrecio ? EstadoIndicador.Atencion : EstadoIndicador.Normal;

    public string ExistenciaNota => Ficha.Minimo > 0 ? $"Mínimo {Ficha.MinimoTexto}" : "Sin mínimo definido";

    public string ValorNota => Ficha.SinPrecio ? "Sin precio con el que valorar" : "Existencia × promedio";

    public string AvisoSinPrecio =>
        "Este material no tiene ninguna compra con precio, así que no hay promedio con el que valorarlo. " +
        "Todo proceso de producción que lo consuma queda marcado como costo incompleto.";

    public string NotaEntradasSinPrecio =>
        "Las entradas sin precio (ajustes de inventario, recepciones de otro origen) suman a la existencia " +
        "pero no al promedio: valorarlas a cero lo arrastraría hacia abajo sin ser un costo real.";

    protected override bool Validar(out string? error)
    {
        error = null;
        return true;
    }
}
