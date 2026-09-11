using System.Collections.Generic;

namespace ASO_PRODUCS.Desktop.Configuration;

/// <summary>
/// Catálogo sugerido para una planta láctea: los productos, tipos de materia prima y etapas de
/// producción típicos de fabricar queso, queso amarillo, mantequilla, crema de leche y suero.
///
/// Es contenido específico de ESTE negocio, no del scaffold genérico — si la planta real trabaja
/// otros productos o pasos, este es el único archivo que hay que editar. Lo consumen los botones
/// "Cargar sugeridos" de Materia Prima · Existencias, Procesos · Producción (pestaña Etapas) y
/// Procesos · Despacho (pestaña Productos).
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
    /// Pasteurización→Enfriado→Descremado→Envasado. El <c>Orden</c> deja huecos de 10 para poder
    /// insertar etapas nuevas después sin renumerar las existentes.
    /// </summary>
    public static IReadOnlyList<(string Nombre, string Descripcion, int Orden)> EtapasProduccion { get; } =
    [
        ("Pasteurización", "Calentamiento de la leche para eliminar patógenos antes de procesar.", 10),
        ("Enfriado", "Bajar la leche pasteurizada a la temperatura de trabajo antes de inocular o cuajar.", 20),
        ("Inoculación", "Adición del fermento/cultivo láctico.", 30),
        ("Cuajado", "Adición del cuajo y reposo hasta formar la cuajada.", 40),
        ("Corte de cuajada", "División de la cuajada en granos para facilitar el desuerado.", 50),
        ("Desuerado", "Separación del suero líquido de la cuajada sólida.", 60),
        ("Moldeado", "Vaciado de la cuajada en moldes para darle forma.", 70),
        ("Prensado", "Prensado del queso moldeado para compactarlo y terminar de expulsar el suero.", 80),
        ("Salado", "Salado en seco o en salmuera.", 90),
        ("Maduración", "Reposo controlado para desarrollar sabor y textura (quesos madurados, como el Amarillo).", 100),
        ("Descremado", "Separación de la nata/crema de la leche entera.", 110),
        ("Batido", "Batido de la crema hasta separar la mantequilla del suero de mantequilla.", 120),
        ("Amasado y lavado", "Lavado y amasado de la mantequilla para retirar el suero de mantequilla residual.", 130),
        ("Envasado", "Empacado del producto terminado.", 140),
    ];
}
