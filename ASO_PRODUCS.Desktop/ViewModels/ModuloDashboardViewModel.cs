using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Controls;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Una cifra del resumen de un módulo.
///
/// <paramref name="Estado"/> es lo que decide el color. Sin él las tarjetas se pintaban iguales
/// y había que leerlas una a una para enterarse de que algo iba mal.
/// </summary>
public sealed record Indicador(
    string Etiqueta,
    string Valor,
    string Nota,
    EstadoIndicador Estado = EstadoIndicador.Normal);

/// <summary>
/// Resumen de un módulo: sus indicadores y el acceso a cada submódulo.
///
/// Los indicadores se calculan <b>fuera del hilo de interfaz</b>. Calcularlos en el constructor,
/// de forma síncrona y sin caché, congelaría la ventana en cada navegación sin decir por qué.
/// </summary>
public sealed class ModuloDashboardViewModel : ViewModelBase, IRecargable
{
    public event EventHandler<Submodulo>? SubmoduloSolicitado;

    public Modulo Modulo { get; }
    public ObservableCollection<Indicador> Indicadores { get; } = [];
    public IReadOnlyList<Submodulo> Submodulos { get; }

    public ICommand AbrirSubmoduloCommand { get; }

    private bool _cargando = true;

    /// <summary>Mientras dura, las tarjetas muestran un esqueleto en vez de un valor en blanco.</summary>
    public bool Cargando
    {
        get => _cargando;
        private set => SetProperty(ref _cargando, value);
    }

    private string _errorCarga = string.Empty;

    /// <summary>
    /// Vacío salvo que la consulta falle. Un resumen que no carga tiene que decirlo: en blanco
    /// se confunde con un módulo sin actividad, que es justo lo contrario.
    /// </summary>
    public string ErrorCarga
    {
        get => _errorCarga;
        private set
        {
            if (SetProperty(ref _errorCarga, value))
                OnPropertyChanged(nameof(HayError));
        }
    }

    public bool HayError => ErrorCarga.Length > 0;

    public ModuloDashboardViewModel(Modulo modulo) : this(modulo, SesionActual.Instancia) { }

    public ModuloDashboardViewModel(Modulo modulo, ISesionActual sesion)
    {
        Modulo = modulo;
        Submodulos = sesion.SubmodulosVisibles(modulo);

        AbrirSubmoduloCommand = new RelayCommand<Submodulo>(s => SubmoduloSolicitado?.Invoke(this, s));

        // Cuatro esqueletos mientras llegan los datos: la tarjeta ocupa ya su sitio, asi que la
        // rejilla no salta cuando el valor aparece.
        for (var i = 0; i < 4; i++)
            Indicadores.Add(new Indicador(string.Empty, string.Empty, string.Empty));

        _ = CargarIndicadores();

        _suscripcion = new SuscripcionACambios(Recargar);
    }

    private readonly SuscripcionACambios _suscripcion;

    /// <summary>
    /// Vuelve a calcular los indicadores. Reutiliza la carga en segundo plano de siempre, así que
    /// las tarjetas muestran su esqueleto mientras llegan los valores nuevos.
    /// </summary>
    public void Recargar()
    {
        Cargando = true;
        ErrorCarga = string.Empty;
        _ = CargarIndicadores();
    }

    public void Desconectar() => _suscripcion.Dispose();

    /// <summary>
    /// Trae los indicadores en segundo plano y los publica de vuelta en el hilo de interfaz.
    ///
    /// Cada método de cálculo abre y cierra su propio <c>DbContext</c> (ver
    /// <c>BD/SqlCrudDataSource</c>), así que sacarlos del hilo de interfaz es seguro.
    /// </summary>
    private async Task CargarIndicadores()
    {
        try
        {
            var calculados = await Task.Run(() => CalcularIndicadores(Modulo));

            Indicadores.Clear();
            foreach (var indicador in calculados ?? [])
                Indicadores.Add(indicador);
        }
        catch (Exception ex)
        {
            Indicadores.Clear();
            ErrorCarga = $"No se pudieron calcular los indicadores: {ex.Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    /// <summary>
    /// Indicadores de los módulos que tienen fuente conectada; <c>null</c> en los que no
    /// (Inicio, Peticiones, Administración y Configuración no tienen resumen).
    ///
    /// Las etiquetas se escriben aquí y solo aquí. No están declaradas también en
    /// <see cref="ModuloCatalogo"/>, para que no puedan desincronizarse.
    ///
    /// Cada módulo de negocio nuevo suma su propio caso aquí, siguiendo el mismo patrón — ver
    /// CLAUDE.md.
    /// </summary>
    private static IReadOnlyList<Indicador>? CalcularIndicadores(Modulo modulo) => modulo.Clave switch
    {
        "Finanzas" => CalcularFinanzas(),
        "Inventario" => CalcularInventario(),
        "MateriaPrima" => CalcularMateriaPrima(),
        "Procesos" => CalcularProcesos(),
        _ => null
    };

    /// <summary>Cuenta que crece mal: cero está bien, y a partir de ahí pide atención.</summary>
    private static EstadoIndicador SegunCuenta(int cuantos, int desdeCritico) => cuantos switch
    {
        0 => EstadoIndicador.Normal,
        _ when cuantos >= desdeCritico => EstadoIndicador.Critico,
        _ => EstadoIndicador.Atencion,
    };

    private static IReadOnlyList<Indicador> CalcularFinanzas()
    {
        // SesionActual.Instancia explícito: estos indicadores corren en Task.Run sin sesión
        // propia, y los servicios de dominio la exigen en el constructor. Ninguna llamada de
        // aquí abajo dispara una transición (son consultas), así que ese objeto nunca llega a
        // consultarse — no es el antipatrón del default null, es un singleton visible en el
        // propio call site.
        var sesion = SesionActual.Instancia;

        var banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                     DataSourceFactory.CrearCuentasBancarias(), sesion);

        var pagar = new CuentasPorPagarService(DataSourceFactory.CrearFacturasProveedor(), banco, sesion);
        var cobrar = new CuentasPorCobrarService(DataSourceFactory.CrearFacturasCliente(), banco, sesion);

        var porPagar = pagar.TotalPorPagar();
        var vencido = pagar.TotalVencido();
        var disponible = banco.DisponibleTotal();
        var porCobrar = cobrar.TotalPorCobrar();

        return
        [
            // "Disponible" va PRIMERO a propósito: es dinero que se puede gastar hoy, y no debe
            // confundirse con "Por pagar", que es una deuda.
            new Indicador("Disponible", $"{disponible:N2}", "en las cuentas de la organización",
                disponible <= 0 ? EstadoIndicador.Atencion : EstadoIndicador.Normal),
            new Indicador("Por pagar", $"{porPagar:N2}", "deuda con proveedores"),
            new Indicador("Por cobrar", $"{porCobrar:N2}", "deuda de clientes"),
            new Indicador("Vencido", $"{vencido:N2}", "pagos fuera de plazo",
                vencido > 0 ? EstadoIndicador.Critico : EstadoIndicador.Normal)
        ];
    }

    private static IReadOnlyList<Indicador> CalcularInventario()
    {
        // Mismo criterio que CalcularFinanzas con la sesión: aquí abajo todo son consultas.
        var sesion = SesionActual.Instancia;

        var entradas = DataSourceFactory.CrearEntradasInventario();
        var salidas = DataSourceFactory.CrearSalidasInventario();
        var facturas = DataSourceFactory.CrearFacturasProveedor();

        var inventario = new InventarioService(DataSourceFactory.CrearArticulos(), entradas, salidas);

        var banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                     DataSourceFactory.CrearCuentasBancarias(), sesion);

        var servicioEntradas = new EntradasInventarioService(
            entradas, DataSourceFactory.CrearProveedores(), facturas, inventario,
            new CuentasPorPagarService(facturas, banco, sesion), sesion);

        var servicioSalidas = new SalidasInventarioService(salidas, inventario, sesion);

        var bajoMinimo = inventario.ArticulosBajoMinimo();
        var sinExistencia = inventario.ArticulosSinExistencia();

        return
        [
            // Lo que hay que reponer va primero: es lo único de esta pantalla sobre lo que se
            // actúa hoy. El tamaño del catálogo es contexto, no una alerta.
            new Indicador("Bajo mínimo", $"{bajoMinimo}", "artículos por reponer",
                SegunCuenta(bajoMinimo, 5)),
            new Indicador("Sin existencia", $"{sinExistencia}", "artículos agotados",
                SegunCuenta(sinExistencia, 3)),
            new Indicador("Artículos", $"{inventario.TotalArticulosActivos()}", "activos en el catálogo"),
            new Indicador("Comprado este mes", $"{servicioEntradas.TotalComprasDelMes():N2}",
                $"en {servicioEntradas.DelMes().Count} entradas · {servicioSalidas.DelMes().Count} salidas")
        ];
    }

    private static IReadOnlyList<Indicador> CalcularMateriaPrima()
    {
        var sesion = SesionActual.Instancia;

        var tipos = DataSourceFactory.CrearTiposMateriaPrima();
        var recepciones = DataSourceFactory.CrearRecepcionesMateriaPrima();
        var salidas = DataSourceFactory.CrearSalidasMateriaPrima();

        var materiaPrima = new MateriaPrimaService(tipos, recepciones, salidas);
        var servicioRecepciones = new RecepcionesMateriaPrimaService(recepciones, materiaPrima, sesion);
        var servicioSalidas = new SalidasMateriaPrimaService(salidas, materiaPrima, sesion);

        var sinExistencia = materiaPrima.TiposSinExistencia();

        return
        [
            new Indicador("Sin existencia", $"{sinExistencia}", "tipos agotados",
                SegunCuenta(sinExistencia, 3)),
            new Indicador("Tipos", $"{materiaPrima.TotalTiposActivos()}", "activos en el catálogo"),
            new Indicador("Recepciones este mes", $"{servicioRecepciones.DelMes().Count}",
                $"{servicioSalidas.DelMes().Count} salidas en el mismo período")
        ];
    }

    private static IReadOnlyList<Indicador> CalcularProcesos()
    {
        var sesion = SesionActual.Instancia;

        var procesosDs = DataSourceFactory.CrearProcesosProduccion();
        var productosDs = DataSourceFactory.CrearProductos();
        var despachosDs = DataSourceFactory.CrearDespachos();

        var productos = new ProductosService(productosDs, procesosDs, despachosDs);

        var materiaPrima = new MateriaPrimaService(DataSourceFactory.CrearTiposMateriaPrima(),
            DataSourceFactory.CrearRecepcionesMateriaPrima(), DataSourceFactory.CrearSalidasMateriaPrima());
        var inventario = new InventarioService(DataSourceFactory.CrearArticulos(),
            DataSourceFactory.CrearEntradasInventario(), DataSourceFactory.CrearSalidasInventario());

        var salidasMateriaPrima = new SalidasMateriaPrimaService(DataSourceFactory.CrearSalidasMateriaPrima(), materiaPrima, sesion);
        var salidasInventario = new SalidasInventarioService(DataSourceFactory.CrearSalidasInventario(), inventario, sesion);

        var procesos = new ProcesosProduccionService(procesosDs, productos, salidasMateriaPrima, salidasInventario,
            DataSourceFactory.CrearArticulos(), sesion);

        var facturasClienteDs = DataSourceFactory.CrearFacturasCliente();
        var banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                     DataSourceFactory.CrearCuentasBancarias(), sesion);
        var cuentasPorCobrar = new CuentasPorCobrarService(facturasClienteDs, banco, sesion);

        var despachos = new DespachosService(despachosDs, productos, facturasClienteDs, cuentasPorCobrar, sesion);

        var enProceso = procesos.EnProcesoCount();
        var sinExistencia = productos.ProductosSinExistencia();

        return
        [
            new Indicador("En proceso", $"{enProceso}", "fabricaciones en curso"),
            new Indicador("Sin existencia", $"{sinExistencia}", "productos agotados",
                SegunCuenta(sinExistencia, 3)),
            new Indicador("Terminados este mes", $"{procesos.DelMes().Count(p => p.Estado == EstadoProcesoProduccion.Terminado)}",
                "procesos que cerraron en el período"),
            new Indicador("Despachos este mes", $"{despachos.DelMes().Count}", "salidas de producto terminado")
        ];
    }
}
