using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Facturas de venta pendientes de cobro. Sub-listado de Finanzas · Cuentas por Cobrar; el
/// encabezado lo pone <see cref="CuentasPorCobrarViewModel"/>. Calco de
/// <see cref="FacturasProveedorCrudViewModel"/> con Cliente en vez de Proveedor.
/// </summary>
public sealed class FacturasClienteCrudViewModel : CrudViewModelBase<FacturaCliente, int>
{
    private const string FiltroTodas = "Todas";

    private readonly IClienteDataSource _clientes;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly CuentasPorCobrarService _servicio;
    private readonly MovimientosService _banco;

    private string _filtroEstado = FiltroTodas;

    public FacturasClienteCrudViewModel(IFacturaClienteDataSource facturas,
                                        IClienteDataSource clientes,
                                        IServicioDialogo dialogos,
                                        ISesionActual sesion)
        : base(facturas, dialogos, sesion)
    {
        _clientes = clientes;
        _dialogos = dialogos;
        _sesionActual = sesion;
        _banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                  DataSourceFactory.CrearCuentasBancarias(), sesion);
        _servicio = new CuentasPorCobrarService(facturas, _banco, sesion);

        CambiarFiltroEstadoCommand = new RelayCommand<string>(filtro =>
        {
            _filtroEstado = filtro;
            ItemsView.Refresh();
        });

        RegistrarCobroCommand = new RelayCommand(RegistrarCobro,
            () => SelectedItem is { } f && _servicio.PuedeRegistrarCobro(f) && _sesionActual.Puede(Permisos.Finanzas.Cobrar));

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } f && _servicio.PuedeAnular(f) && _sesionActual.Puede(Permisos.Finanzas.Anular));

        VerDetalleCommand = new RelayCommand(VerDetalle);
    }

    public ICommand CambiarFiltroEstadoCommand { get; }
    public ICommand RegistrarCobroCommand { get; }
    public ICommand AnularCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla (ver <c>CuentasPorCobrarView.xaml.cs</c>),
    /// nunca un botón — por eso no lleva <c>CanExecute</c>: la guarda de "hay algo seleccionado"
    /// vive dentro de <see cref="VerDetalle"/>. Es una consulta de solo lectura, así que funciona
    /// igual sin importar el <see cref="EstadoFacturaCliente"/> de la factura.</summary>
    public ICommand VerDetalleCommand { get; }

    /// <summary>Estado de lo por cobrar, visible sin ir al dashboard del módulo.</summary>
    public string ResumenDeuda =>
        $"Por cobrar {_servicio.TotalPorCobrar():N2} · vencido {_servicio.TotalVencido():N2}";

    protected override string ModuloPermiso => "FacturasCliente";

    protected override bool CoincideBusqueda(FacturaCliente item, string texto) =>
        item.ClienteNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroDocumento.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(FacturaCliente item) => _filtroEstado switch
    {
        "Pendientes" => item.Estado == EstadoFacturaCliente.Pendiente,
        "Vencidas" => item.EstaVencida,
        "Cobradas" => item.Estado == EstadoFacturaCliente.Cobrada,
        "Anuladas" => item.Estado == EstadoFacturaCliente.Anulada,
        _ => true
    };

    protected override bool PuedeEditar(FacturaCliente item) => _servicio.PuedeEditar(item);

    protected override bool PuedeEliminar(FacturaCliente item) => _servicio.PuedeEliminar(item);

    protected override FacturaCliente CrearNuevo() => new()
    {
        FechaEmision = DateTime.Today,
        FechaVencimiento = DateTime.Today.AddDays(30),
        Estado = EstadoFacturaCliente.Pendiente,
        CreadoPorId = _sesionActual.UsuarioActual?.Id ?? 0,
        FechaCreacion = DateTime.Now
    };

    protected override CrudEditorViewModelBase<FacturaCliente> CrearEditor(FacturaCliente item) =>
        new FacturaClienteEditorViewModel(item, _clientes, _servicio);

    /// <summary>
    /// Pregunta a qué cuenta entró el dinero y lo anota en el libro de banco, además de dar la
    /// factura por cobrada.
    /// </summary>
    private void RegistrarCobro()
    {
        if (SelectedItem is not { } factura)
            return;

        var editor = new AsientoBancoEditorViewModel(
            $"Registrar cobro de la factura Nº {factura.NumeroDocumento}",
            $"{factura.ClienteNombre} — {factura.Descripcion}",
            factura.Monto,
            esEntrada: true,
            _banco.CuentasActivas(),
            "Registrar cobro");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.RegistrarCobro(factura, editor.Resultado,
                                               _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } factura)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular factura {factura.NumeroDocumento}",
            $"{factura.ClienteNombre} — {factura.Descripcion} — {factura.MontoTexto}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(factura, editor.Motivo));
    }

    private void VerDetalle()
    {
        if (SelectedItem is not { } factura)
            return;

        _dialogos.MostrarEditor(new FacturaClienteDetalleViewModel(factura));
    }

    /// <summary>
    /// La lista la repuebla la recarga que dispara la escritura del servicio; aquí solo se
    /// apunta qué factura dejar seleccionada.
    /// </summary>
    private void Aplicar(Func<FacturaCliente> transicion)
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
}

/// <summary>
/// La ficha de "ver detalle" de una factura de cliente: solo lectura, se abre con doble clic
/// sobre la fila (ver <c>CuentasPorCobrarView.xaml.cs</c>). Calco de
/// <see cref="FacturaProveedorDetalleViewModel"/> con Cliente en vez de Proveedor.
/// </summary>
public sealed class FacturaClienteDetalleViewModel : CrudEditorViewModelBase
{
    public FacturaClienteDetalleViewModel(FacturaCliente factura)
    {
        Factura = factura;
    }

    public FacturaCliente Factura { get; }

    public override string Titulo => $"Factura {Factura.NumeroDocumento}";

    /// <summary>Amplio: lleva la grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Cerrar";

    public override bool MuestraCancelar => false;

    protected override bool Validar(out string? error)
    {
        error = null;
        return true;
    }
}

/// <summary>
/// Alta/edición de una factura de venta. La validación de fondo (número repetido, monto,
/// vencimiento) la hace <see cref="CuentasPorCobrarService"/>: el editor se la pide y muestra el
/// mensaje que devuelva. Calco de <see cref="FacturaProveedorEditorViewModel"/>.
/// </summary>
public sealed class FacturaClienteEditorViewModel : CrudEditorViewModelBase<FacturaCliente>
{
    private readonly FacturaCliente _original;
    private readonly CuentasPorCobrarService _servicio;

    public FacturaClienteEditorViewModel(FacturaCliente original,
                                         IClienteDataSource clientes,
                                         CuentasPorCobrarService servicio)
    {
        _original = original;
        _servicio = servicio;

        Clientes = clientes.GetAll().Where(c => c.Activo).OrderBy(c => c.Nombre).ToList();

        NumeroDocumento = original.NumeroDocumento;
        Descripcion = original.Descripcion;
        FechaEmision = original.FechaEmision == default ? DateTime.Today : original.FechaEmision;
        FechaVencimiento = original.FechaVencimiento ?? DateTime.Today.AddDays(30);
        Monto = original.Monto == 0 ? string.Empty : original.Monto.ToString("0.##");

        ClienteSeleccionado = Clientes.FirstOrDefault(c => c.Id == original.ClienteId);
    }

    public override string Titulo =>
        _original.Id == 0 ? "Registrar factura de cliente" : $"Editar factura Nº {_original.Id}";

    public override double AnchoEditor => Ancho.Estandar;

    public IReadOnlyList<Cliente> Clientes { get; }

    private Cliente? _clienteSeleccionado;
    public Cliente? ClienteSeleccionado
    {
        get => _clienteSeleccionado;
        set => SetProperty(ref _clienteSeleccionado, value);
    }

    private string _numeroDocumento = string.Empty;
    public string NumeroDocumento
    {
        get => _numeroDocumento;
        set => SetProperty(ref _numeroDocumento, value);
    }

    private string _descripcion = string.Empty;
    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    private DateTime _fechaEmision = DateTime.Today;
    public DateTime FechaEmision
    {
        get => _fechaEmision;
        set => SetProperty(ref _fechaEmision, value);
    }

    private DateTime _fechaVencimiento = DateTime.Today.AddDays(30);
    public DateTime FechaVencimiento
    {
        get => _fechaVencimiento;
        set => SetProperty(ref _fechaVencimiento, value);
    }

    private string _monto = string.Empty;
    public string Monto
    {
        get => _monto;
        set => SetProperty(ref _monto, value);
    }

    protected override bool Validar(out string? error)
    {
        if (!decimal.TryParse(Monto, out _))
        {
            error = "El monto debe ser un número.";
            return false;
        }

        return _servicio.Validar(Construir(), out error);
    }

    public override FacturaCliente ObtenerResultado() => Construir();

    private FacturaCliente Construir()
    {
        var factura = _original.Clonar();

        factura.NumeroDocumento = NumeroDocumento.Trim();
        factura.ClienteId = ClienteSeleccionado?.Id ?? 0;
        factura.ClienteNombre = ClienteSeleccionado?.Nombre ?? string.Empty;
        factura.Descripcion = Descripcion.Trim();
        factura.FechaEmision = FechaEmision;
        factura.FechaVencimiento = FechaVencimiento;
        factura.Monto = decimal.TryParse(Monto, out var monto) ? monto : 0m;

        return factura;
    }
}

/// <summary>
/// Finanzas · Cuentas por Cobrar: las facturas de venta pendientes de cobro. El maestro de
/// clientes vive aparte, en Finanzas · Clientes (<see cref="ClientesViewModel"/>).
/// </summary>
public sealed class CuentasPorCobrarViewModel : PantallaViewModelBase
{
    public CuentasPorCobrarViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private CuentasPorCobrarViewModel(Modulo modulo,
                                      Submodulo submodulo,
                                      IServicioDialogo dialogos,
                                      ISesionActual sesion)
        : base(modulo, submodulo)
    {
        Facturas = new FacturasClienteCrudViewModel(
            DataSourceFactory.CrearFacturasCliente(), DataSourceFactory.CrearClientes(),
            dialogos, sesion);
    }

    public FacturasClienteCrudViewModel Facturas { get; }

    public override void Recargar() => Facturas.Recargar();
}
