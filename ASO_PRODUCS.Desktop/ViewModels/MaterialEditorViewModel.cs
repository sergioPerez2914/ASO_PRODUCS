using System.Collections.Generic;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Alta/edición de un material maestro: un artículo del almacén o un tipo de materia prima.
///
/// Eran dos formularios que no se parecían en nada —uno con código, categoría, ubicación y notas
/// sobre un enum cerrado de unidades heredado de ASO_RTR; el otro con nombre, unidad de texto y
/// mínimo— para la misma operación. Ahora los dos campos, los dos textos de ayuda y las dos
/// validaciones viven aquí, y las subclases solo aportan lo que es de su módulo.
///
/// Es genérico porque <c>CrudViewModelBase.CrearEditor</c> devuelve
/// <c>CrudEditorViewModelBase&lt;Articulo&gt;</c> / <c>&lt;TipoMateriaPrima&gt;</c>; el que los dos
/// modelos sean <see cref="IMaterialMaestro"/> es lo que deja leerlos y escribirlos sin conocerlos.
///
/// El código puede escribirse a mano o dejarse en blanco: si se deja, se genera al validar y se
/// escribe en el campo, para que quien lo dio de alta lo vea antes de que la ventana se cierre.
/// </summary>
public abstract class MaterialEditorViewModel<T> : CrudEditorViewModelBase<T>
    where T : IEntidad<int>, IMaterialMaestro
{
    private readonly T _original;

    protected MaterialEditorViewModel(T original)
    {
        _original = original;

        Codigo = original.Codigo;
        Nombre = original.Nombre;
        UnidadMedida = original.UnidadMedida;
        Minimo = original.Minimo == 0 ? string.Empty : original.Minimo.ToString("0.##");
        Activo = original.Id == 0 || original.Activo;

        GenerarCodigoCommand = new RelayCommand(() => Codigo = GenerarCodigo());
    }

    public override double AnchoEditor => Ancho.Estandar;

    public ICommand GenerarCodigoCommand { get; }

    /// <summary>Lista sugerida, no cerrada: el desplegable se puede escribir.</summary>
    public IReadOnlyList<string> Unidades { get; } = UnidadesSugeridas.Todas;

    /// <summary>Qué es esto para el usuario ("artículo", "tipo de materia prima"), para los textos
    /// que comparten los dos formularios.</summary>
    protected abstract string QueEs { get; }

    protected abstract string GenerarCodigo();

    protected abstract bool ValidarEnServicio(T material, out string? error);

    /// <summary>Copia del original sobre la que estampar lo editado, sin tocar el que está en la
    /// lista. Lo aporta la subclase porque <c>Clonar()</c> no está en ninguna interfaz común.</summary>
    protected abstract T Clonar(T original);

    public override string Titulo => _original.Id == 0
        ? $"Nuevo {QueEs}"
        : $"Editar {_original.Nombre}";

    public string AyudaInactivo =>
        $"Un {QueEs} inactivo se conserva en el historial, pero no se ofrece al registrar " +
        "entradas ni salidas. La existencia no se escribe aquí: sale de lo que entra y lo que sale.";

    private string _codigo = string.Empty;
    public string Codigo
    {
        get => _codigo;
        set => SetProperty(ref _codigo, value);
    }

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _unidadMedida = string.Empty;
    public string UnidadMedida
    {
        get => _unidadMedida;
        set => SetProperty(ref _unidadMedida, value);
    }

    private string _minimo = string.Empty;
    public string Minimo
    {
        get => _minimo;
        set => SetProperty(ref _minimo, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error)
    {
        if (!string.IsNullOrWhiteSpace(Minimo)
            && (!decimal.TryParse(Minimo, out var minimo) || minimo < 0))
        {
            error = "El mínimo debe ser un número mayor o igual a cero.";
            return false;
        }

        // Se genera ANTES de validar y se deja escrito en el campo: así el usuario ve con qué
        // código se guardó, en vez de enterarse al volver al listado.
        if (string.IsNullOrWhiteSpace(Codigo))
            Codigo = GenerarCodigo();

        return ValidarEnServicio(ObtenerResultado(), out error);
    }

    public override T ObtenerResultado()
    {
        var material = Clonar(_original);
        material.Codigo = Codigo.Trim();
        material.Nombre = Nombre.Trim();
        material.UnidadMedida = UnidadMedida.Trim();
        material.Minimo = decimal.TryParse(Minimo, out var minimo) ? minimo : 0m;
        material.Activo = Activo;
        return material;
    }
}
