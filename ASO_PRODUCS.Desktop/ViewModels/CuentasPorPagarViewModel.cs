using System;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Facturas de compra pendientes de pago. Sub-listado de Finanzas · Cuentas por Pagar; el
/// encabezado y el conmutador los pone <see cref="CuentasPorPagarViewModel"/>.
/// </summary>
public sealed class FacturasProveedorCrudViewModel : CrudViewModelBase<FacturaProveedor, int>
{
    private const string FiltroTodas = "Todas";

    private readonly IProveedorDataSource _proveedores;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly CuentasPorPagarService _servicio;
    private readonly MovimientosService _banco;

    private string _filtroEstado = FiltroTodas;

    public FacturasProveedorCrudViewModel(IFacturaProveedorDataSource facturas,
                                          IProveedorDataSource proveedores,
                                          IServicioDialogo dialogos,
                                          ISesionActual sesion)
        : base(facturas, dialogos, sesion)
    {
        _proveedores = proveedores;
        _dialogos = dialogos;
        _sesionActual = sesion;
        _banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                  DataSourceFactory.CrearCuentasBancarias(), sesion);
        _servicio = new CuentasPorPagarService(facturas, _banco, sesion);

        CambiarFiltroEstadoCommand = new RelayCommand<string>(filtro =>
        {
            _filtroEstado = filtro;
            ItemsView.Refresh();
        });

        RegistrarPagoCommand = new RelayCommand(RegistrarPago,
            () => SelectedItem is { } f && _servicio.PuedeRegistrarPago(f) && _sesionActual.Puede("Finanzas.Pagar"));

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } f && _servicio.PuedeAnular(f) && _sesionActual.Puede("Finanzas.Anular"));

        VerDetalleCommand = new RelayCommand(VerDetalle);
    }

    public ICommand CambiarFiltroEstadoCommand { get; }
    public ICommand RegistrarPagoCommand { get; }
    public ICommand AnularCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla (ver <c>CuentasPorPagarView.xaml.cs</c>),
    /// nunca un botón — por eso no lleva <c>CanExecute</c>: la guarda de "hay algo seleccionado"
    /// vive dentro de <see cref="VerDetalle"/>. Es una consulta de solo lectura, así que funciona
    /// igual sin importar el <see cref="EstadoFacturaProveedor"/> de la factura.</summary>
    public ICommand VerDetalleCommand { get; }

    /// <summary>Estado de la deuda, visible sin ir al dashboard del módulo.</summary>
    public string ResumenDeuda =>
        $"Por pagar {_servicio.TotalPorPagar():N2} · vencido {_servicio.TotalVencido():N2}";

    protected override string ModuloPermiso => "FacturasProveedor";

    protected override bool CoincideBusqueda(FacturaProveedor item, string texto) =>
        item.ProveedorNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroDocumento.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(FacturaProveedor item) => _filtroEstado switch
    {
        "Pendientes" => item.Estado == EstadoFacturaProveedor.Pendiente,
        "Vencidas" => item.EstaVencida,
        "Pagadas" => item.Estado == EstadoFacturaProveedor.Pagada,
        "Anuladas" => item.Estado == EstadoFacturaProveedor.Anulada,
        _ => true
    };

    protected override bool PuedeEditar(FacturaProveedor item) => _servicio.PuedeEditar(item);

    protected override bool PuedeEliminar(FacturaProveedor item) => _servicio.PuedeEliminar(item);

    protected override FacturaProveedor CrearNuevo() => new()
    {
        FechaEmision = DateTime.Today,
        FechaVencimiento = DateTime.Today.AddDays(30),
        Estado = EstadoFacturaProveedor.Pendiente,
        CreadoPorId = _sesionActual.UsuarioActual?.Id ?? 0,
        FechaCreacion = DateTime.Now
    };

    protected override CrudEditorViewModelBase<FacturaProveedor> CrearEditor(FacturaProveedor item) =>
        new FacturaProveedorEditorViewModel(item, _proveedores, _servicio);

    /// <summary>
    /// Pregunta de qué cuenta salió el dinero y lo anota en el libro de banco, además de dar la
    /// factura por pagada. Ver <see cref="CuentasPorCobrarViewModel"/> para por qué sustituyó al
    /// Confirmar de sí/no que había antes.
    /// </summary>
    private void RegistrarPago()
    {
        if (SelectedItem is not { } factura)
            return;

        var editor = new AsientoBancoEditorViewModel(
            $"Registrar pago de la factura Nº {factura.NumeroDocumento}",
            $"{factura.ProveedorNombre} — {factura.Descripcion}",
            factura.Monto,
            esEntrada: false,
            _banco.CuentasActivas(),
            "Registrar pago");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.RegistrarPago(factura, editor.Resultado,
                                              _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } factura)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular factura {factura.NumeroDocumento}",
            $"{factura.ProveedorNombre} — {factura.Descripcion} — {factura.MontoTexto}",
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

        _dialogos.MostrarEditor(new FacturaProveedorDetalleViewModel(factura));
    }

    /// <summary>
    /// La lista la repuebla la recarga que dispara la escritura del servicio; aquí solo se
    /// apunta qué factura dejar seleccionada.
    /// </summary>
    private void Aplicar(Func<FacturaProveedor> transicion)
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
/// La ficha de "ver detalle" de una factura de proveedor: solo lectura, se abre con doble clic
/// sobre la fila (ver <c>CuentasPorPagarView.xaml.cs</c>). Expone la <see cref="FacturaProveedor"/>
/// completa en vez de repetir cada propiedad como envoltorio, mismo criterio que
/// <see cref="ProcesoDetalleViewModel"/>.
/// </summary>
public sealed class FacturaProveedorDetalleViewModel : CrudEditorViewModelBase
{
    public FacturaProveedorDetalleViewModel(FacturaProveedor factura)
    {
        Factura = factura;
    }

    public FacturaProveedor Factura { get; }

    public override string Titulo => $"Factura {Factura.NumeroDocumento}";

    /// <summary>Amplio: lleva la grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Cerrar";

    /// <summary>Ficha de solo lectura: no hay nada que cancelar — ver el comentario de
    /// <see cref="CrudEditorViewModelBase.MuestraCancelar"/>.</summary>
    public override bool MuestraCancelar => false;

    protected override bool Validar(out string? error)
    {
        error = null;
        return true;
    }
}

/// <summary>
/// Finanzas · Cuentas por Pagar: las facturas de compra pendientes de pago. El maestro de
/// proveedores vive aparte, en Finanzas · Proveedores (<see cref="ProveedoresViewModel"/>).
/// </summary>
public sealed class CuentasPorPagarViewModel : PantallaViewModelBase
{
    public CuentasPorPagarViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private CuentasPorPagarViewModel(Modulo modulo,
                                     Submodulo submodulo,
                                     IServicioDialogo dialogos,
                                     ISesionActual sesion)
        : base(modulo, submodulo)
    {
        Facturas = new FacturasProveedorCrudViewModel(
            DataSourceFactory.CrearFacturasProveedor(), DataSourceFactory.CrearProveedores(),
            dialogos, sesion);
    }

    public FacturasProveedorCrudViewModel Facturas { get; }

    public override void Recargar() => Facturas.Recargar();
}
