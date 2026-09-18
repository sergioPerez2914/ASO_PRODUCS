using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Controls;

namespace ASO_PRODUCS.Desktop.Navigation;

public sealed record Submodulo(string Clave, string Nombre, string Descripcion, string Icono)
{
    /// <summary>
    /// Permiso de navegación, DERIVADO de la clave en vez de declarado aparte: así no puede
    /// desincronizarse al renombrar un submódulo. Ej.: "Ver.Finanzas.CuentasPorPagar".
    /// </summary>
    public string Permiso => Services.Permisos.Ver(Clave);
}

public sealed record Modulo(
    string Clave,
    string Nombre,
    string Descripcion,
    string Icono,
    IReadOnlyList<Submodulo> Submodulos)
{
    /// <summary>Permiso propio. Solo decide por sí mismo en los módulos SIN submódulos
    /// (Inicio, Peticiones); en el resto la visibilidad la deciden sus submódulos.</summary>
    public string Permiso => Services.Permisos.Ver(Clave);
}

/// <summary>
/// Fuente única de la estructura de navegación. El sidebar, el lanzador de inicio, los
/// dashboards y el enrutado de MainWindow leen de aquí, así que agregar o renombrar un
/// submódulo se hace en un solo lugar.
///
/// PROVISIONAL: trae los módulos heredados del scaffold — Finanzas (Cuentas por Pagar,
/// Movimientos y Proveedores), Inventario y Materia Prima — como plantilla viva de los patrones
/// del framework (ver "Cómo se agrega un submódulo" en CLAUDE.md). Los módulos propios de los
/// productores se agregan aquí siguiendo esa receta.
/// </summary>
public static class ModuloCatalogo
{
    public static Modulo Inicio { get; } = new(
        "Inicio",
        "Inicio",
        "Punto de entrada a los módulos del sistema.",
        Iconos.Inicio,
        []);

    /// <summary>
    /// Bandeja de peticiones de cambio. Es un pseudo-módulo fijado en el menú, igual que
    /// <see cref="Inicio"/>: no cuelga de ningún módulo de negocio porque las peticiones
    /// atraviesan todos.
    /// </summary>
    public static Modulo Peticiones { get; } = new(
        "Peticiones",
        "Peticiones",
        "Solicitudes de cambio pendientes de aprobación.",
        Iconos.Peticiones,
        []);

    public static IReadOnlyList<Modulo> Modulos { get; } =
    [
        new Modulo(
            "Finanzas",
            "Finanzas",
            "Pagos a proveedores y libro de banco.",
            Iconos.Finanzas,
            [
                new Submodulo("Finanzas.CuentasPorPagar", "Cuentas por Pagar",
                    "Obligaciones con proveedores y su vencimiento.", Iconos.CuentasPorPagar),
                new Submodulo("Finanzas.CuentasPorCobrar", "Cuentas por Cobrar",
                    "Lo que los clientes deben a la organización y su vencimiento.", Iconos.CuentasPorCobrar),
                new Submodulo("Finanzas.Movimientos", "Movimientos",
                    "Estado de la cuenta según lo cobrado y pagado en la aplicación.", Iconos.Banco),
                new Submodulo("Finanzas.Proveedores", "Proveedores",
                    "Maestro de proveedores de la organización.", Iconos.Proveedor),
                new Submodulo("Finanzas.Clientes", "Clientes",
                    "Maestro de clientes a los que se les despacha producto terminado.", Iconos.Cliente)
            ]),

        new Modulo(
            "Inventario",
            "Inventario",
            "Existencias del almacén, entradas y salidas.",
            Iconos.Inventario,
            [
                new Submodulo("Inventario.Almacen", "Almacén",
                    "Existencias de todo lo que la organización guarda en el almacén.", Iconos.Almacen),
                new Submodulo("Inventario.Entradas", "Entradas",
                    "Historial de entradas y registro de lo que llega al almacén.", Iconos.Entradas),
                new Submodulo("Inventario.Salidas", "Salidas",
                    "Historial de salidas y emisión de boletos de salida.", Iconos.Salidas)
            ]),

        new Modulo(
            "MateriaPrima",
            "Materia Prima",
            "Existencia, recepción y salida de la materia prima de los productores.",
            Iconos.MateriaPrima,
            [
                new Submodulo("MateriaPrima.Recepciones", "Recepciones",
                    "Historial de recepciones y registro de lo que llega.", Iconos.Entradas),
                new Submodulo("MateriaPrima.Existencias", "Existencias",
                    "Cuánto hay de cada tipo de materia prima, de un vistazo.", Iconos.Almacen),
                new Submodulo("MateriaPrima.Salidas", "Salidas",
                    "Historial de salidas y registro de lo que se despacha.", Iconos.Salidas)
            ]),

        new Modulo(
            "Procesos",
            "Procesos",
            "Fabricación de producto terminado y su despacho.",
            Iconos.Procesos,
            [
                new Submodulo("Procesos.Produccion", "Producción",
                    "Etapas de fabricación y los procesos que van transformando materia prima y artículos en producto terminado.", Iconos.Produccion),
                // El icono es el de Almacén, igual que Inventario · Almacén y Materia Prima ·
                // Existencias: los tres son lo mismo —un catálogo con su existencia derivada— y
                // antes este llevaba el de Despacho, que además es el mismo glifo que Pedidos.
                new Submodulo("Procesos.ProductosYLotes", "Productos y Lotes",
                    "Catálogo de productos terminados y su existencia por lote.", Iconos.Almacen),
                new Submodulo("Procesos.Pedidos", "Pedidos",
                    "Lo que los clientes pidieron, antes de despacharlo.", Iconos.Pedidos),
                new Submodulo("Procesos.Despachos", "Despachos",
                    "Historial de lo que salió hacia el cliente y registro de ajustes de existencia.", Iconos.Despacho)
            ]),

        new Modulo(
            "Reportes",
            "Reportes",
            "Reportes de producción, ventas, gastos y cartera.",
            Iconos.Reportes,
            [
                new Submodulo("Reportes.Procesos", "Reporte de Procesos",
                    "Producción, rendimiento y consumo de insumos por período.", Iconos.Procesos),
                new Submodulo("Reportes.Ventas", "Reporte de Ventas",
                    "Lo vendido, cobrado y pendiente por período.", Iconos.CuentasPorCobrar),
                new Submodulo("Reportes.Gastos", "Reporte de Gastos",
                    "Salidas de banco por período y por categoría.", Iconos.CuentasPorPagar),
                new Submodulo("Reportes.Cartera", "Reporte de Cartera",
                    "Antigüedad de saldos por cliente y por proveedor.", Iconos.CuentasPorCobrar)
            ]),
    ];

    /// <summary>
    /// Administración del sistema: usuarios, permisos y los datos de la propia organización.
    /// Fijado como <see cref="Peticiones"/>, y por el mismo motivo: no pertenece a ningún
    /// módulo de negocio.
    /// </summary>
    public static Modulo Administracion { get; } = new(
        "Administracion",
        "Administración",
        "Usuarios, roles, permisos y los datos de la organización.",
        Iconos.Administracion,
        []);

    /// <summary>
    /// Preferencias de quien usa la aplicación en esta máquina: apariencia, la propia cuenta y
    /// los ajustes de la app. Es un módulo fijado más, pero NO entra en <see cref="Fijados"/>
    /// porque esa lista es el orden del menú de arriba y este va anclado al pie del sidebar,
    /// donde se busca lo que no forma parte del trabajo del día.
    /// </summary>
    public static Modulo Configuracion { get; } = new(
        "Configuracion",
        "Configuración",
        "Apariencia, tu cuenta y las preferencias de la aplicación.",
        Iconos.Configuracion,
        []);

    /// <summary>Los módulos fijados que se listan arriba, en orden de menú.</summary>
    public static IReadOnlyList<Modulo> Fijados { get; } = [Inicio, Peticiones, Administracion];

    /// <summary>
    /// Todo lo fijado, incluida <see cref="Configuracion"/>, que se pinta aparte. Es la lista
    /// que hay que mirar para preguntas de permisos y de resolución de claves: si se usara
    /// <see cref="Fijados"/>, "Ver.Configuracion" no existiría en la matriz y no habría forma
    /// de quitarle la sección a nadie.
    /// </summary>
    public static IReadOnlyList<Modulo> TodosLosFijados { get; } = [.. Fijados, Configuracion];

    public static Modulo? BuscarModulo(string clave)
        => TodosLosFijados.FirstOrDefault(m => m.Clave == clave)
           ?? Modulos.FirstOrDefault(m => m.Clave == clave);

    /// <summary>Resuelve un submódulo por su clave completa ("Modulo.Submodulo").</summary>
    public static Submodulo? BuscarSubmodulo(string clave)
        => Modulos.SelectMany(m => m.Submodulos).FirstOrDefault(s => s.Clave == clave);
}
