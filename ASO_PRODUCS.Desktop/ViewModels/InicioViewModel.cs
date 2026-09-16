using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>Una alerta del panel de Inicio: cuántas cosas necesitan atención y a qué submódulo
/// lleva verlas. <paramref name="Submodulo"/> puede ser nulo si el destino es el módulo entero.</summary>
public sealed record AlertaResumen(string Titulo, int Cantidad, string Detalle, Modulo Modulo, Submodulo? Submodulo);

/// <summary>
/// Lanzador: tarjeta por módulo con su lista de submódulos, más una tarjeta de alertas arriba
/// (bajo mínimo de los tres catálogos, lotes por vencer) con acceso directo a cada una.
///
/// Las alertas se calculan fuera del hilo de interfaz, mismo motivo que
/// <see cref="ModuloDashboardViewModel"/>: cada fuente de datos abre su propio <c>DbContext</c>,
/// así que sacarlas del hilo de interfaz es seguro y evita congelar la ventana al entrar.
/// </summary>
public sealed class InicioViewModel : ViewModelBase, IRecargable
{
    public event EventHandler<Modulo>? ModuloSolicitado;
    public event EventHandler<NavegacionEventArgs>? SubmoduloSolicitado;

    public IReadOnlyList<Modulo> Modulos { get; }
    public ObservableCollection<AlertaResumen> Alertas { get; } = [];

    public bool TieneAlertas => Alertas.Count > 0;

    public ICommand AbrirModuloCommand { get; }
    public ICommand AbrirAlertaCommand { get; }

    private readonly SuscripcionACambios _suscripcion;

    public InicioViewModel() : this(SesionActual.Instancia) { }

    public InicioViewModel(ISesionActual sesion)
    {
        Modulos = sesion.ModulosVisibles();
        AbrirModuloCommand = new RelayCommand<Modulo>(m => ModuloSolicitado?.Invoke(this, m));

        AbrirAlertaCommand = new RelayCommand<AlertaResumen>(a =>
        {
            if (a is not null)
                SubmoduloSolicitado?.Invoke(this, new NavegacionEventArgs(a.Modulo, a.Submodulo));
        });

        _ = CargarAlertas();
        _suscripcion = new SuscripcionACambios(Recargar);
    }

    public void Recargar() => _ = CargarAlertas();

    public void Desconectar() => _suscripcion.Dispose();

    private async Task CargarAlertas()
    {
        try
        {
            var calculadas = await Task.Run(CalcularAlertas);

            Alertas.Clear();
            foreach (var alerta in calculadas)
                Alertas.Add(alerta);

            OnPropertyChanged(nameof(TieneAlertas));
        }
        catch
        {
            // Inicio no tiene un canal de error propio (a diferencia de ModuloDashboardViewModel):
            // si el cálculo falla, la tarjeta de alertas simplemente no aparece, sin bloquear el
            // resto del lanzador.
        }
    }

    private static IReadOnlyList<AlertaResumen> CalcularAlertas()
    {
        var articulosDs = DataSourceFactory.CrearArticulos();
        var tiposDs = DataSourceFactory.CrearTiposMateriaPrima();
        var productosDs = DataSourceFactory.CrearProductos();
        var procesosDs = DataSourceFactory.CrearProcesosProduccion();
        var despachosDs = DataSourceFactory.CrearDespachos();

        var inventario = new InventarioService(articulosDs,
            DataSourceFactory.CrearEntradasInventario(), DataSourceFactory.CrearSalidasInventario());
        var materiaPrima = new MateriaPrimaService(tiposDs,
            DataSourceFactory.CrearRecepcionesMateriaPrima(), DataSourceFactory.CrearSalidasMateriaPrima());
        var productos = new ProductosService(productosDs, procesosDs, despachosDs);

        var moduloInventario = ModuloCatalogo.BuscarModulo("Inventario")!;
        var moduloMateriaPrima = ModuloCatalogo.BuscarModulo("MateriaPrima")!;
        var moduloProcesos = ModuloCatalogo.BuscarModulo("Procesos")!;

        var alertas = new List<AlertaResumen>();

        void Agregar(string titulo, int cantidad, string detalle, Modulo modulo, Submodulo? submodulo)
        {
            if (cantidad > 0)
                alertas.Add(new AlertaResumen(titulo, cantidad, detalle, modulo, submodulo));
        }

        Agregar("Artículos bajo mínimo", inventario.ArticulosBajoMinimo(), "en Inventario · Almacén",
            moduloInventario, ModuloCatalogo.BuscarSubmodulo("Inventario.Almacen"));

        Agregar("Materia prima bajo mínimo", materiaPrima.TiposBajoMinimo(), "en Materia Prima · Existencias",
            moduloMateriaPrima, ModuloCatalogo.BuscarSubmodulo("MateriaPrima.Existencias"));

        Agregar("Productos bajo mínimo", productos.ProductosBajoMinimo(), "en Procesos · Despacho",
            moduloProcesos, ModuloCatalogo.BuscarSubmodulo("Procesos.Despacho"));

        var porVencer = productos.LotesConExistencia().Count(l => l.Estado == EstadoVencimientoLote.PorVencer);
        Agregar("Lotes por vencer", porVencer, "en los próximos días · Procesos · Despacho",
            moduloProcesos, ModuloCatalogo.BuscarSubmodulo("Procesos.Despacho"));

        return alertas;
    }
}
