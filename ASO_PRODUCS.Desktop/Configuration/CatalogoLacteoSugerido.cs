using System.Collections.Generic;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Configuration;

/// <summary>
/// Catálogo sugerido para una planta láctea: los productos, tipos de materia prima y etapas de
/// producción típicos de fabricar queso, queso amarillo, mantequilla, crema de leche y suero.
///
/// Es contenido específico de ESTE negocio, no del scaffold genérico — si la planta real trabaja
/// otros productos o pasos, este es el único archivo que hay que editar. Las unidades salen del
/// vocabulario de <see cref="UnidadesSugeridas"/>, que es lo que proponen los formularios. Lo consumen los botones
/// "Cargar sugeridos" de Inventario · Almacén, Materia Prima · Existencias, Procesos · Producción
/// (pestaña Etapas) y Procesos · Productos y Lotes (pestaña Productos).
/// </summary>
public static class CatalogoLacteoSugerido
{
    public static IReadOnlyList<(string Nombre, string UnidadMedida)> Productos { get; } =
    [
        ("Queso", "kg"),
        ("Queso Amarillo", "kg"),
        ("Mantequilla", "kg"),
        ("Crema de Leche", "L"),
        ("Suero", "L"),
    ];

    /// <summary>
    /// Insumos de almacén: lo que no se transforma en el producto pero hace falta para
    /// despacharlo.
    ///
    /// El código NO va aquí: lo genera <c>InventarioService.GenerarCodigo()</c> al crear cada
    /// uno, igual que cuando se da de alta a mano dejando el campo vacío.
    /// </summary>
    public static IReadOnlyList<(string Nombre, string UnidadMedida)> Articulos { get; } =
    [
        ("Bolsa de empaque", "Pieza"),
        ("Etiqueta de producto", "Pieza"),
        ("Molde para queso", "Pieza"),
        ("Caja de cartón", "Caja"),
        ("Detergente alcalino", "L"),
        ("Desinfectante de superficies", "L"),
        ("Guantes desechables", "Caja"),
        ("Gorro desechable", "Caja"),
    ];

    public static IReadOnlyList<(string Nombre, string UnidadMedida)> TiposMateriaPrima { get; } =
    [
        ("Leche cruda", "L"),
        ("Fermento láctico", "g"),
        ("Cuajo", "mL"),
        ("Sal", "kg"),
        ("Cloruro de calcio", "mL"),
    ];

    /// <summary>
    /// Catálogo único y reutilizable de etapas: no hay una secuencia guardada por producto, el
    /// operador arma cada proceso a mano. Referencia de qué elegir para cada uno: Queso =
    /// Pasteurización→Enfriado→Inoculación→Cuajado→Corte de cuajada→Desuerado→Moldeado→Prensado→
    /// Salado→Envasado. Queso Amarillo = igual + Maduración antes de Envasado. Mantequilla =
    /// Pasteurización→Enfriado→Descremado→Batido→Amasado y lavado→Envasado. Crema de Leche =
    /// Pasteurización→Enfriado→Descremado→Envasado.
    /// </summary>
    public static IReadOnlyList<(string Nombre, string Descripcion)> EtapasProduccion { get; } =
    [
        ("Pasteurización", "Calentamiento de la leche para eliminar patógenos antes de procesar."),
        ("Enfriado", "Bajar la leche pasteurizada a la temperatura de trabajo antes de inocular o cuajar."),
        ("Inoculación", "Adición del fermento/cultivo láctico."),
        ("Cuajado", "Adición del cuajo y reposo hasta formar la cuajada."),
        ("Corte de cuajada", "División de la cuajada en granos para facilitar el desuerado."),
        ("Desuerado", "Separación del suero líquido de la cuajada sólida."),
        ("Moldeado", "Vaciado de la cuajada en moldes para darle forma."),
        ("Prensado", "Prensado del queso moldeado para compactarlo y terminar de expulsar el suero."),
        ("Salado", "Salado en seco o en salmuera."),
        ("Maduración", "Reposo controlado para desarrollar sabor y textura (quesos madurados, como el Amarillo)."),
        ("Descremado", "Separación de la nata/crema de la leche entera."),
        ("Batido", "Batido de la crema hasta separar la mantequilla del suero de mantequilla."),
        ("Amasado y lavado", "Lavado y amasado de la mantequilla para retirar el suero de mantequilla residual."),
        ("Envasado", "Empacado del producto terminado."),
    ];
}
