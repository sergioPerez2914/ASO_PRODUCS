using System.Collections.Generic;

namespace ASO_PRODUCS.Desktop.Configuration;

/// <summary>
/// Las unidades que proponen los desplegables de los catálogos de materiales.
///
/// Es una SUGERENCIA, no un catálogo cerrado: el campo sigue siendo texto libre y el desplegable
/// se puede escribir. Existe porque, con tres catálogos escribiendo la unidad a mano
/// (<c>Articulo</c>, <c>TipoMateriaPrima</c>, <c>Producto</c>), el vocabulario ya se había ido
/// cada uno por su lado — el propio <see cref="CatalogoLacteoSugerido"/> sembraba "Kg" en
/// Productos y "Kilogramos" en Materia Prima para la misma unidad.
///
/// Lo que ya está guardado se respeta tal cual: un tipo que diga "Litros" sigue diciéndolo, solo
/// deja de ser lo que la lista propone.
/// </summary>
public static class UnidadesSugeridas
{
    public static IReadOnlyList<string> Todas { get; } =
    [
        "kg",
        "g",
        "L",
        "mL",
        "Unidad",
        "Caja",
        "Saco",
        "Bolsa",
        "Pieza"
    ];
}
