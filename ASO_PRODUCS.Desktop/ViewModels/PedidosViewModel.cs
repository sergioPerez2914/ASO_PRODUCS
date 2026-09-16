using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Procesos · Pedidos: lo que los clientes pidieron, antes de despacharlo. No hay conmutador con
/// otro catálogo —a diferencia de Despacho— así que esta es la pantalla directamente, mismo
/// arquetipo que <see cref="RecepcionesMateriaPrimaViewModel"/>: Editar/Eliminar apagados,
/// "Nuevo pedido" pasa por el servicio de dominio, y "Anular" en vez de borrar.
///
/// Además de eso, trae la acción que no tiene ningún otro documento del scaffold: "Despachar
/// pedido" abre un <see cref="DespachoEditorViewModel"/> PRECARGADO con lo que falta entregar
/// (ver <see cref="DespachoEditorViewModel.PrecargarDesdePedido"/>) y arma, para eso, la misma
/// cadena de dependencias que ya construye <see cref="DespachosViewModel"/> — duplicarla aquí es
/// el mismo criterio que ya usa <see cref="CostosProduccionService"/>, construido por separado en
/// más de una pantalla.
/// </summary>
public sealed class PedidosViewModel : PantallaCrudViewModel<Pedido, int>
{
    private const string FiltroTodos = "Todos";

    private readonly PedidosService _servicio;
    private readonly IProductoDataSource _productosDs;
    private readonly IClienteDataSource _clientesDs;
    private readonly IDespachoDataSource _despachosDs;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;

    private string _filtro = FiltroTodos;

    public PedidosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearPedidos(), DataSourceFactory.CrearDespachos(),
               DataSourceFactory.CrearProductos(), DataSourceFactory.CrearClientes(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private PedidosViewModel(Modulo modulo,
                             Submodulo submodulo,
                             IPedidoDataSource pedidos,
                             IDespachoDataSource despachosDs,
                             IProductoDataSource productosDs,
                             IClienteDataSource clientesDs,
                             IServicioDialogo dialogos,
                             ISesionActual sesion)
        : base(modulo, submodulo, pedidos, dialogos, sesion)
    {
        _servicio = new PedidosService(pedidos, despachosDs, sesion);
        _productosDs = productosDs;
        _clientesDs = clientesDs;
        _despachosDs = despachosDs;
        _dialogos = dialogos;
        _sesionActual = sesion;

        // La base ya pobló Items en su constructor, pero sin lo despachado: sin esto la primera
        // pintada saldría con todo "Pendiente" hasta la primera recarga.
        _servicio.RellenarDespachado(Items);
        ItemsView.Refresh();

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } p && _servicio.PuedeAnular(p) && _sesionActual.Puede(Permisos.Pedidos.Anular));

        DespacharCommand = new RelayCommand(Despachar,
            () => SelectedItem is { } p && _servicio.PuedeDespachar(p) && _sesionActual.Puede(Permisos.Despachos.Crear));

        VerDetalleCommand = new RelayCommand(VerDetalle);
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }
    public ICommand DespacharCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla, nunca un botón — mismo criterio que
    /// <see cref="DespachosCrudViewModel.VerDetalleCommand"/>.</summary>
    public ICommand VerDetalleCommand { get; }

    public string Resumen =>
        $"{Items.Count(p => p.Estado == EstadoPedido.Registrado)} pedidos · " +
        $"{Items.Count(p => p.Estado == EstadoPedido.Registrado && p.EstadoEntregaTexto != "Completado")} pendientes";

    protected override string ModuloPermiso => "Pedidos";

    protected override bool CoincideBusqueda(Pedido item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ClienteNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.ProductoNombre.Contains(texto, StringComparison.OrdinalIgnoreCase));

    protected override bool PasaFiltroExtra(Pedido item) => _filtro switch
    {
        "Pendientes" => item.Estado == EstadoPedido.Registrado && item.EstadoEntregaTexto == "Pendiente",
        "Parciales" => item.Estado == EstadoPedido.Registrado && item.EstadoEntregaTexto == "Parcial",
        "Completados" => item.Estado == EstadoPedido.Registrado && item.EstadoEntregaTexto == "Completado",
        "Anulados" => item.Estado == EstadoPedido.Anulado,
        _ => true
    };

    protected override bool PuedeEditar(Pedido item) => false;

    protected override bool PuedeEliminar(Pedido item) => false;

    protected override Pedido CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Estado = EstadoPedido.Registrado
    };

    protected override CrudEditorViewModelBase<Pedido> CrearEditor(Pedido item) =>
        new PedidoEditorViewModel(item, [.. _productosDs.GetActivos().OrderBy(p => p.Nombre)], ListaClientes());

    private IReadOnlyList<Cliente> ListaClientes() =>
        [.. _clientesDs.GetAll().Where(c => c.Activo).OrderBy(c => c.Nombre)];

    /// <summary>
    /// Registrar pasa por el servicio de dominio, igual que Despachos/Procesos: valida y asigna
    /// el correlativo.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(), _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } pedido)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular pedido {pedido.Numero}",
            $"{pedido.ClienteNombre} — {pedido.EstadoMostrado}. Anular no revierte los despachos que ya " +
            "haya generado este pedido; solo dice que no hay que despachar más de él.",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(pedido, editor.Motivo));
    }

    /// <summary>
    /// Abre el despacho ya lleno con lo que falta entregar de este pedido. Arma la misma cadena
    /// de servicios que <see cref="DespachosViewModel"/>, fresca en cada apertura (productos con
    /// existencia, lotes con existencia, clientes) para que el despacho vea el estado real.
    /// </summary>
    private void Despachar()
    {
        if (SelectedItem is not { } pedido)
            return;

        var productosServicio = new ProductosService(_productosDs, DataSourceFactory.CrearProcesosProduccion(), _despachosDs);

        var banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                     DataSourceFactory.CrearCuentasBancarias(), _sesionActual);
        var cuentasPorCobrar = new CuentasPorCobrarService(DataSourceFactory.CrearFacturasCliente(), banco, _sesionActual);

        var despachosServicio = new DespachosService(_despachosDs, productosServicio,
            DataSourceFactory.CrearFacturasCliente(), cuentasPorCobrar, _sesionActual);

        var editor = new DespachoEditorViewModel(
            new Despacho { Fecha = DateTime.Today, Estado = EstadoDespacho.Registrado },
            productosServicio.ActivosConExistencia(), ListaClientes(), productosServicio.LotesConExistencia(),
            _sesionActual.UsuarioActual?.NombreCompleto ?? string.Empty, despachosServicio);

        editor.PrecargarDesdePedido(pedido);

        if (!_dialogos.MostrarEditor(editor))
            return;

        try
        {
            despachosServicio.Registrar(editor.ObtenerResultado(), _sesionActual.UsuarioActual?.Id ?? 0);
            Recargar();
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
    }

    private void VerDetalle()
    {
        if (SelectedItem is not { } pedido)
            return;

        _dialogos.MostrarEditor(new PedidoDetalleViewModel(pedido));
    }

    private void Aplicar(Func<Pedido> transicion)
    {
        try
        {
            SeleccionarTrasRecargar(transicion().Id);
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
    }

    /// <summary>Relee el listado y le vuelve a pegar lo despachado encima — mismo criterio que
    /// <see cref="ProductosCrudViewModel.Recargar"/> con la existencia.</summary>
    public override void Recargar()
    {
        base.Recargar();

        _servicio.RellenarDespachado(Items);
        ItemsView.Refresh();
        OnTodasLasPropiedadesCambiaron();
    }
}

/// <summary>Alta de un pedido: cliente, líneas con precio y observaciones. Un pedido no se edita
/// una vez registrado —se anula y se crea uno nuevo—, así que no hay modo "editar" que atender,
/// a diferencia de <see cref="DespachoEditorViewModel"/>.</summary>
public sealed class PedidoEditorViewModel : CrudEditorViewModelBase<Pedido>
{
    private readonly Pedido _original;

    public PedidoEditorViewModel(Pedido original, IReadOnlyList<Producto> productos, IReadOnlyList<Cliente> clientes)
    {
        _original = original;

        Productos = productos;
        Clientes = clientes;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        Observaciones = original.Observaciones;
        ClienteSeleccionado = Clientes.FirstOrDefault(c => c.Id == original.ClienteId);

        Lineas.CollectionChanged += AlCambiarLineas;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaPedidoEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    public override string Titulo => "Nuevo pedido";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Registrar pedido";

    public IReadOnlyList<Producto> Productos { get; }
    public IReadOnlyList<Cliente> Clientes { get; }

    public ObservableCollection<LineaPedidoEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private Cliente? _clienteSeleccionado;
    public Cliente? ClienteSeleccionado
    {
        get => _clienteSeleccionado;
        set => SetProperty(ref _clienteSeleccionado, value);
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    public decimal TotalCantidad => Lineas.Sum(l => l.CantidadValor);
    public decimal Total => Lineas.Sum(l => l.Subtotal);
    public string TotalTexto => Total.ToString("N2");

    protected override bool Validar(out string? error)
    {
        if (ClienteSeleccionado is null)
        {
            error = "Seleccione el cliente que hace el pedido.";
            return false;
        }

        if (Lineas.Any(l => l.ProductoSeleccionado is not null && !l.CantidadEsValida))
        {
            error = "Hay una cantidad que no es un número válido.";
            return false;
        }

        if (Lineas.Any(l => l.ProductoSeleccionado is not null && !l.PrecioEsValido))
        {
            error = "Hay un precio que no es un número válido.";
            return false;
        }

        error = null;
        return true;
    }

    public override Pedido ObtenerResultado()
    {
        var pedido = _original.Clonar();
        pedido.Fecha = Fecha.Date;
        pedido.ClienteId = ClienteSeleccionado?.Id ?? 0;
        pedido.ClienteNombre = ClienteSeleccionado?.Nombre ?? string.Empty;
        pedido.Observaciones = Observaciones.Trim();

        pedido.Lineas = [.. Lineas
            .Where(l => l.ProductoSeleccionado is not null)
            .Select(l => l.Construir())];

        pedido.Total = pedido.Lineas.Sum(l => l.Subtotal);

        return pedido;
    }

    private LineaPedidoEditorViewModel NuevaLinea()
    {
        var linea = new LineaPedidoEditorViewModel(Productos);
        linea.Cambio += AlCambiarLinea;
        return linea;
    }

    private void AlCambiarLineas(object? remitente, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        foreach (var linea in e.OldItems?.OfType<LineaPedidoEditorViewModel>() ?? [])
            linea.Cambio -= AlCambiarLinea;

        AlCambiarLinea(this, EventArgs.Empty);
    }

    private void AlCambiarLinea(object? remitente, EventArgs e)
    {
        OnPropertyChanged(nameof(TotalCantidad));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalTexto));
    }
}

/// <summary>Un renglón del pedido mientras se escribe. Más simple que
/// <see cref="LineaDespachoEditorViewModel"/>: no hay lote (el pedido no reserva existencia) ni
/// modo Ajuste (un pedido siempre lleva precio).</summary>
public sealed class LineaPedidoEditorViewModel : ViewModelBase
{
    public event EventHandler? Cambio;

    public LineaPedidoEditorViewModel(IReadOnlyList<Producto> productos)
    {
        Productos = productos;
    }

    public IReadOnlyList<Producto> Productos { get; }

    private Producto? _productoSeleccionado;
    public Producto? ProductoSeleccionado
    {
        get => _productoSeleccionado;
        set
        {
            if (!SetProperty(ref _productoSeleccionado, value))
                return;

            if (value is { PrecioUnitario: > 0 } && string.IsNullOrWhiteSpace(PrecioUnitario))
                PrecioUnitario = value.PrecioUnitario.ToString("0.####");

            Recalcular();
        }
    }

    private string _cantidad = string.Empty;
    public string Cantidad
    {
        get => _cantidad;
        set { if (SetProperty(ref _cantidad, value)) Recalcular(); }
    }

    private string _precioUnitario = string.Empty;
    public string PrecioUnitario
    {
        get => _precioUnitario;
        set { if (SetProperty(ref _precioUnitario, value)) Recalcular(); }
    }

    public bool CantidadEsValida =>
        !string.IsNullOrWhiteSpace(Cantidad) && decimal.TryParse(Cantidad, out var v) && v > 0;

    public bool PrecioEsValido =>
        !string.IsNullOrWhiteSpace(PrecioUnitario) && decimal.TryParse(PrecioUnitario, out var v) && v > 0;

    public decimal CantidadValor => decimal.TryParse(Cantidad, out var valor) ? valor : 0;
    public decimal PrecioValor => decimal.TryParse(PrecioUnitario, out var valor) ? valor : 0m;
    public decimal Subtotal => CantidadValor * PrecioValor;
    public string SubtotalTexto => Subtotal.ToString("N2");

    public PedidoLinea Construir() => new()
    {
        ProductoId = ProductoSeleccionado!.Id,
        ProductoNombre = ProductoSeleccionado.Nombre,
        UnidadMedidaSnapshot = ProductoSeleccionado.UnidadMedida,
        CantidadPedida = CantidadValor,
        PrecioUnitario = PrecioValor,
        Subtotal = Subtotal
    };

    private void Recalcular()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalTexto));
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Ficha de solo lectura de un pedido: cabecera + líneas con Pedido/Despachado/Pendiente
/// y el chip de <see cref="Pedido.EstadoMostrado"/>. Mismo criterio que
/// <see cref="DespachoDetalleViewModel"/>.</summary>
public sealed class PedidoDetalleViewModel : CrudEditorViewModelBase
{
    public PedidoDetalleViewModel(Pedido pedido)
    {
        Pedido = pedido;
    }

    public Pedido Pedido { get; }

    public override string Titulo => $"Pedido {Pedido.Numero}";

    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Cerrar";

    public override bool MuestraCancelar => false;

    protected override bool Validar(out string? error)
    {
        error = null;
        return true;
    }
}
