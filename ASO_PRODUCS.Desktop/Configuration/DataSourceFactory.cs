using ASO_PRODUCS.Desktop.BD;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.Configuration;

/// <summary>
/// Punto UNICO de composicion de las fuentes de datos.
///
/// Solo hay un camino: SQL Server via EF Core. La fabrica es el sitio donde los ViewModels
/// resuelven sus dependencias sin conocer la implementacion, y donde se da de alta una
/// entidad nueva.
///
/// Las fuentes Sql son sin estado (abren un <see cref="AsoProductoresDbContext"/> por metodo), asi que
/// se cachean con <c>??=</c> por ahorro, no por correccion: el ambito lo lee el contexto al
/// construirse, no la fuente.
/// </summary>
public static class DataSourceFactory
{
    private static IProveedorDataSource? _proveedores;
    private static IFacturaProveedorDataSource? _facturasProveedor;
    private static ICuentaBancariaDataSource? _cuentasBancarias;
    private static IMovimientoBancoDataSource? _movimientosBanco;
    private static IOrganizacionDataSource? _organizaciones;
    private static IUsuarioDataSource? _usuarios;
    private static IPermisoUsuarioDataSource? _permisosUsuario;
    private static IPeticionCambioDataSource? _peticiones;
    private static IArticuloDataSource? _articulos;
    private static IEntradaInventarioDataSource? _entradasInventario;
    private static ISalidaInventarioDataSource? _salidasInventario;
    private static ITipoMateriaPrimaDataSource? _tiposMateriaPrima;
    private static IRecepcionMateriaPrimaDataSource? _recepcionesMateriaPrima;
    private static ISalidaMateriaPrimaDataSource? _salidasMateriaPrima;
    private static IEtapaProduccionDataSource? _etapasProduccion;
    private static IProductoDataSource? _productos;
    private static IProcesoProduccionDataSource? _procesosProduccion;
    private static IDespachoDataSource? _despachos;
    private static IClienteDataSource? _clientes;
    private static IFacturaClienteDataSource? _facturasCliente;
    private static IAuthService? _auth;
    private static IAjustesStore? _ajustesStore;

    public static IProveedorDataSource CrearProveedores() =>
        _proveedores ??= new SqlProveedorDataSource();

    public static IFacturaProveedorDataSource CrearFacturasProveedor() =>
        _facturasProveedor ??= new SqlFacturaProveedorDataSource();

    public static ICuentaBancariaDataSource CrearCuentasBancarias() =>
        _cuentasBancarias ??= new SqlCuentaBancariaDataSource();

    public static IMovimientoBancoDataSource CrearMovimientosBanco() =>
        _movimientosBanco ??= new SqlMovimientoBancoDataSource();

    public static IOrganizacionDataSource CrearOrganizaciones() =>
        _organizaciones ??= new SqlOrganizacionDataSource();

    public static IUsuarioDataSource CrearUsuarios() =>
        _usuarios ??= new SqlUsuarioDataSource();

    public static IPermisoUsuarioDataSource CrearPermisosUsuario() =>
        _permisosUsuario ??= new SqlPermisoUsuarioDataSource();

    public static IPeticionCambioDataSource CrearPeticiones() =>
        _peticiones ??= new SqlPeticionCambioDataSource();

    public static IArticuloDataSource CrearArticulos() =>
        _articulos ??= new SqlArticuloDataSource();

    public static IEntradaInventarioDataSource CrearEntradasInventario() =>
        _entradasInventario ??= new SqlEntradaInventarioDataSource();

    public static ISalidaInventarioDataSource CrearSalidasInventario() =>
        _salidasInventario ??= new SqlSalidaInventarioDataSource();

    public static ITipoMateriaPrimaDataSource CrearTiposMateriaPrima() =>
        _tiposMateriaPrima ??= new SqlTipoMateriaPrimaDataSource();

    public static IRecepcionMateriaPrimaDataSource CrearRecepcionesMateriaPrima() =>
        _recepcionesMateriaPrima ??= new SqlRecepcionMateriaPrimaDataSource();

    public static ISalidaMateriaPrimaDataSource CrearSalidasMateriaPrima() =>
        _salidasMateriaPrima ??= new SqlSalidaMateriaPrimaDataSource();

    public static IEtapaProduccionDataSource CrearEtapasProduccion() =>
        _etapasProduccion ??= new SqlEtapaProduccionDataSource();

    public static IProductoDataSource CrearProductos() =>
        _productos ??= new SqlProductoDataSource();

    public static IProcesoProduccionDataSource CrearProcesosProduccion() =>
        _procesosProduccion ??= new SqlProcesoProduccionDataSource();

    public static IDespachoDataSource CrearDespachos() =>
        _despachos ??= new SqlDespachoDataSource();

    public static IClienteDataSource CrearClientes() =>
        _clientes ??= new SqlClienteDataSource();

    public static IFacturaClienteDataSource CrearFacturasCliente() =>
        _facturasCliente ??= new SqlFacturaClienteDataSource();

    public static IAuthService CrearAuth() =>
        _auth ??= new AuthService(CrearUsuarios());

    /// <summary>
    /// Preferencias de la maquina. No es una fuente de datos del negocio, pero se compone aqui
    /// por el mismo motivo que las demas: es el unico sitio donde se decide la implementacion.
    /// </summary>
    public static IAjustesStore CrearAjustesStore() =>
        _ajustesStore ??= new AjustesStoreJson();
}
