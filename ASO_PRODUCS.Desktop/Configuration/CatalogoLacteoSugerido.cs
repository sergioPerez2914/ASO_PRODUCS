using System.Collections.Generic;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Configuration;

/// <summary>
/// Catálogo sugerido para una planta láctea: los productos, tipos de materia prima y etapas de
/// producción típicos de fabricar queso, queso amarillo, mantequilla, crema de leche y suero.
///
/// Es contenido específico de ESTE negocio, no del scaffold genérico — si la planta real trabaja
/// otros productos o pasos, este es el único archivo que hay que editar. Lo consumen los botones
/// "Cargar sugeridos" de Inventario · Almacén, Materia Prima · Existencias, Procesos · Producción
/// (pestaña Etapas) y Procesos · Productos y Lotes (pestaña Productos).
/// </summary>
public static class CatalogoLacteoSugerido
{
    public static IReadOnlyList<(string Nombre, string UnidadMedida)> Productos { get; } =
    [
        ("Queso", "Kg"),
        ("Queso Amarillo", "Kg"),
        ("Mantequilla", "Kg"),
        ("Crema de Leche", "Litros"),
        ("Suero", "Litros"),
    ];

    /// <summary>
    /// Insumos de almacén: lo que no se transforma en el producto pero hace falta para
    /// despacharlo. A diferencia de las otras tres listas, esta lleva categoría y unidad
    /// tipada, porque <c>Articulo</c> las tiene y son lo que ordena la grilla del almacén.
    ///
    /// El código NO va aquí: lo genera <c>InventarioService.GenerarCodigo()</c> al crear cada
    /// uno, igual que cuando se da de alta a mano dejando el campo vacío.
    /// </summary>
    public static IReadOnlyList<(string Nombre, string Categoria, UnidadMedida Unidad)> Articulos { get; } =
    [
        ("Bolsa de empaque", "Empaque", UnidadMedida.Pieza),
        ("Etiqueta de producto", "Empaque", UnidadMedida.Pieza),
        ("Molde para queso", "Utensilio", UnidadMedida.Pieza),
        ("Caja de cartón", "Empaque", UnidadMedida.Caja),
        ("Detergente alcalino", "Limpieza", UnidadMedida.Litro),
        ("Desinfectante de superficies", "Limpieza", UnidadMedida.Litro),
        ("Guantes desechables", "Higiene", UnidadMedida.Caja),
        ("Gorro desechable", "Higiene", UnidadMedida.Caja),
    ];

    public static IReadOnlyList<(string Nombre, string UnidadMedida)> TiposMateriaPrima { get; } =
    [
        ("Leche cruda", "Litros"),
        ("Fermento láctico", "Gramos"),
        ("Cuajo", "Mililitros"),
        ("Sal", "Kilogramos"),
        ("Cloruro de calcio", "Mililitros"),
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
