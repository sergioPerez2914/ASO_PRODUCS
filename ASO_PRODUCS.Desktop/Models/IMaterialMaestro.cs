namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Lo que tienen en común los dos catálogos de materiales que se cuentan: los artículos del
/// almacén (<see cref="Articulo"/>) y los tipos de materia prima (<see cref="TipoMateriaPrima"/>).
///
/// Dar de alta uno u otro es la misma operación —un material con su existencia derivada— y hasta
/// ahora eran dos formularios que no se parecían en nada. Esta interfaz es lo que deja que
/// <c>MaterialEditorViewModel</c> lea y escriba los cinco campos sin conocer la entidad concreta.
///
/// <see cref="Producto"/> NO la implementa a propósito: su formulario lleva además precio, vida
/// útil, presentación y componentes — no es el mismo formulario.
/// </summary>
public interface IMaterialMaestro
{
    /// <summary>Código con el que la organización identifica el material. Único dentro de la
    /// organización; si quien lo da de alta no escribe uno, lo genera el servicio del módulo.</summary>
    string Codigo { get; set; }

    string Nombre { get; set; }

    /// <summary>Unidad en que se cuenta (kg, L, Caja…). Texto libre con una lista sugerida
    /// (<c>UnidadesSugeridas</c>): mientras no haya una necesidad real de un catálogo cerrado, no
    /// vale la pena mantenerlo.</summary>
    string UnidadMedida { get; set; }

    /// <summary>Existencia por debajo de la cual el material se marca en su pantalla. Cero
    /// significa "no vigilar".</summary>
    decimal Minimo { get; set; }

    bool Activo { get; set; }
}
