using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Que puede cada rol. Es el conjunto BASE; el administrador lo ajusta por usuario con
/// <see cref="PermisoUsuario"/> y la suma la resuelve <see cref="SesionActual.IniciarSesion"/>.
///
/// El universo de permisos se arma por reflexion sobre <see cref="Permisos"/> y sobre el
/// catalogo de navegacion, a proposito: agregar un permiso o un submodulo lo mete solo en
/// "todos", de modo que un rol total no se queda corto por olvido.
///
/// PROVISIONAL: los 4 roles son genéricos (ver <see cref="Rol"/>) y los conjuntos base solo
/// cubren los módulos heredados del scaffold (Finanzas, Inventario, Materia Prima). Ajustar en
/// cuanto el negocio real tenga sus propios módulos y roles.
/// </summary>
public static class MatrizPermisos
{
    /// <summary>Todos los permisos de accion declarados en <see cref="Permisos"/>.</summary>
    private static readonly HashSet<string> _acciones =
        typeof(Permisos).GetNestedTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

    /// <summary>
    /// Los permisos "Ver.*" que de verdad se consultan, derivados del catalogo de navegacion:
    /// los modulos fijados (Inicio, Peticiones, Administracion, Configuracion) y todos los
    /// submodulos.
    ///
    /// Los modulos de negocio NO entran: su visibilidad la deciden sus submodulos (ver
    /// <see cref="NavegacionPermitida"/>), asi que "Ver.Finanzas" no se lee nunca.
    /// </summary>
    private static readonly HashSet<string> _navegacion =
        ModuloCatalogo.TodosLosFijados.Select(m => m.Permiso)
            .Concat(ModuloCatalogo.Modulos.SelectMany(m => m.Submodulos).Select(s => s.Permiso))
            .ToHashSet();

    public static IReadOnlySet<string> Todos { get; } =
        _acciones.Concat(_navegacion).ToHashSet();

    /// <summary>
    /// Reservado al Desarrollador: repartir su propio rol. Un administrador manda en su
    /// organización, pero no fabrica usuarios con más alcance del que el mismo tiene.
    /// </summary>
    private static readonly HashSet<string> _soloDesarrollador =
    [
        Permisos.Usuarios.CrearDesarrollador
    ];

    /// <summary>
    /// El día a día en Finanzas: registra facturas de proveedor y proveedores nuevos, pero no
    /// mueve dinero (eso es de Supervisor) ni borra nada. Para lo demás, petición.
    /// </summary>
    private static readonly HashSet<string> _operador =
    [
        Permisos.Ver(ModuloCatalogo.Inicio.Clave),
        Permisos.Ver(ModuloCatalogo.Peticiones.Clave),
        Permisos.Ver(ModuloCatalogo.Configuracion.Clave),
        Permisos.Ver("Finanzas.CuentasPorPagar"),
        Permisos.Ver("Finanzas.CuentasPorCobrar"),
        Permisos.Ver("Finanzas.Movimientos"),
        Permisos.Ver("Finanzas.Proveedores"),
        Permisos.Ver("Finanzas.Clientes"),
        Permisos.Ver("Inventario.Almacen"),
        Permisos.Ver("Inventario.Entradas"),
        Permisos.Ver("Inventario.Salidas"),
        Permisos.Ver("MateriaPrima.Existencias"),
        Permisos.Ver("MateriaPrima.Recepciones"),
        Permisos.Ver("MateriaPrima.Salidas"),
        Permisos.Ver("Procesos.Produccion"),
        Permisos.Ver("Procesos.Despacho"),

        Permisos.Proveedores.Crear,
        Permisos.Proveedores.Editar,

        Permisos.FacturasProveedor.Crear,
        Permisos.FacturasProveedor.Editar,

        Permisos.Clientes.Crear,
        Permisos.Clientes.Editar,

        Permisos.FacturasCliente.Crear,
        Permisos.FacturasCliente.Editar,

        Permisos.Articulos.Crear,
        Permisos.Articulos.Editar,

        Permisos.EntradasInventario.Crear,
        Permisos.SalidasInventario.Crear,

        Permisos.TiposMateriaPrima.Crear,
        Permisos.TiposMateriaPrima.Editar,

        Permisos.RecepcionesMateriaPrima.Crear,
        Permisos.SalidasMateriaPrima.Crear,

        Permisos.EtapasProduccion.Crear,
        Permisos.EtapasProduccion.Editar,

        Permisos.Productos.Crear,
        Permisos.Productos.Editar,

        Permisos.ProcesosProduccion.Crear,
        Permisos.ProcesosProduccion.AgregarEtapa,
        Permisos.ProcesosProduccion.Terminar,

        Permisos.Despachos.Crear,

        Permisos.Peticiones.Solicitar

        // Fuera a propósito:
        // - Finanzas.Pagar / Movimientos.*: mover dinero es de Supervisor.
        // - Proveedores.Eliminar / FacturasProveedor.Eliminar / Articulos.Eliminar: borrar es
        //   de Supervisor.
        // - EntradasInventario.Anular / SalidasInventario.Anular: deshacer un documento del
        //   almacén es de Supervisor, igual que deshacer uno de Finanzas.
        // - RecepcionesMateriaPrima.Anular / SalidasMateriaPrima.Anular /
        //   TiposMateriaPrima.Eliminar: mismo criterio que Inventario.
        // - EtapasProduccion.Eliminar / Productos.Eliminar / ProcesosProduccion.Anular /
        //   Despachos.Anular: mismo criterio, deshacer y borrar es de Supervisor.
        // - Clientes.Eliminar / FacturasCliente.Eliminar / Finanzas.Cobrar: cobrar mueve dinero,
        //   igual que Finanzas.Pagar, así que queda con Supervisor.
    ];

    /// <summary>
    /// Supervisa Finanzas: todo lo de Operador, más registrar el pago (que además asienta el
    /// movimiento en el libro de banco) y administrar las cuentas bancarias. No deshace un
    /// movimiento ya asentado — eso es del administrador de la organización.
    /// </summary>
    private static readonly HashSet<string> _supervisor =
        _operador.Concat(
        [
            Permisos.Proveedores.Eliminar,
            Permisos.FacturasProveedor.Eliminar,

            Permisos.Clientes.Eliminar,
            Permisos.FacturasCliente.Eliminar,

            Permisos.Finanzas.Pagar,
            Permisos.Finanzas.Cobrar,
            Permisos.Finanzas.Anular,

            Permisos.Movimientos.Crear,
            Permisos.Movimientos.Editar,
            Permisos.Movimientos.Eliminar,
            Permisos.Movimientos.Conciliar,
            Permisos.Movimientos.Transferir,

            Permisos.CuentasBancarias.Crear,
            Permisos.CuentasBancarias.Editar,

            Permisos.Articulos.Eliminar,

            Permisos.EntradasInventario.Anular,
            Permisos.SalidasInventario.Anular,

            Permisos.TiposMateriaPrima.Eliminar,
            Permisos.RecepcionesMateriaPrima.Anular,
            Permisos.SalidasMateriaPrima.Anular,

            Permisos.EtapasProduccion.Eliminar,
            Permisos.Productos.Eliminar,
            Permisos.ProcesosProduccion.Anular,
            Permisos.Despachos.Anular,

            Permisos.Peticiones.Resolver

            // Fuera a propósito:
            // - Movimientos.Anular: deshacer un movimiento ya asentado queda un nivel más arriba.
        ]).ToHashSet();

    private static readonly HashSet<string> _administrador =
        Todos.Where(p => !_soloDesarrollador.Contains(p)).ToHashSet();

    /// <summary>Conjunto base del rol, antes de los ajustes por usuario.</summary>
    // Sin arco de descarte, y es deliberado: con `_ => Todos` un rol nuevo que se olvide aqui
    // se convierte en superusuario EN SILENCIO. Ahora el compilador avisa (CS8509) en cuanto se
    // declare un rol y no se mapee. Se calla CS8524, que solo se dispararia con un entero que no
    // corresponde a ningun miembro: fila corrupta, y ahi preferimos la excepcion al disimulo.
#pragma warning disable CS8524
    public static IReadOnlySet<string> Base(Rol rol) => rol switch
    {
        Rol.Operador => _operador,
        Rol.Supervisor => _supervisor,
        Rol.AdministradorOrganizacion => _administrador,
        Rol.Desarrollador => Todos
    };
#pragma warning restore CS8524

    /// <summary>
    /// Permisos que, si faltan, no dejan el boton muerto: abren una peticion al administrador.
    ///
    /// Empieza vacía: hoy no hay ninguna acción sensible gateada en las pantallas que un
    /// Operador ve. Al ampliar lo que ve un rol, sumar aquí la acción sensible correspondiente
    /// y cablear el comando con <see cref="SolicitudesDeCambio"/>, o la regla queda muerta.
    /// </summary>
    public static IReadOnlySet<string> Solicitables { get; } = new HashSet<string>();

    public static bool EsSolicitable(string permiso) => Solicitables.Contains(permiso);
}
