using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Procesos · Despachos: el historial de lo que salió y el registro de un Ajuste (la única
/// forma de despacho que sigue creándose a mano; una Venta solo nace de "Despachar pedido", ver
/// <see cref="PedidosCrudViewModel.DespacharCommand"/>).
///
/// Misma forma que Materia Prima · Salidas: las filas son documentos, así que no se editan ni se
/// borran, se anulan. Anular devuelve la existencia sola, porque el kardex de productos ignora
/// lo anulado. Desde que un despacho de tipo Venta acopla con Finanzas, necesita además el
/// padrón de clientes para ofrecerlo en el editor (aunque, siendo Ajuste-only el alta a mano, en
/// la práctica esa lista no se usa desde este editor).
/// </summary>
public sealed class DespachosCrudViewModel : CrudViewModelBase<Despacho, int>
{
    private const string FiltroTodos = "Todos";

    private readonly ProductosService _productos;
    private readonly IReadOnlyList<Cliente> _clientes;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly DespachosService _servicio;

    private string _filtro = FiltroTodos;

    public DespachosCrudViewModel(IDespachoDataSource despachos,
                                  ProductosService productos,
                                  IClienteDataSource clientes,
                                  DespachosService servicio,
                                  IServicioDialogo dialogos,
                                  ISesionActual sesion)
        : base(despachos, dialogos, sesion)
    {
        _productos = productos;
        _clientes = [.. clientes.GetAll().Where(c => c.Activo).OrderBy(c => c.Nombre)];
        _servicio = servicio;
        _dialogos = dialogos;
        _sesionActual = sesion;

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } d && _servicio.PuedeAnular(d) && _sesionActual.Puede(Permisos.Despachos.Anular));

        VerDetalleCommand = new RelayCommand(VerDetalle);

        ImprimirCommand = new RelayCommand(Imprimir, () => SelectedItem is not null);

        VerFacturaCommand = new RelayCommand(VerFactura,
            () => SelectedItem?.FacturaClienteId is not null);
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla (ver <c>DespachosView.xaml.cs</c>), nunca
    /// un botón — por eso no lleva <c>CanExecute</c>: la guarda de "hay algo seleccionado" vive
    /// dentro de <see cref="VerDetalle"/>. Es una consulta de solo lectura, así que funciona igual
    /// sin importar el <see cref="EstadoDespacho"/> del despacho.</summary>
    public ICommand VerDetalleCommand { get; }

    /// <summary>Saca el documento por la impresora. Ver <see cref="Services.ImpresionDocumento"/>.</summary>
    public ICommand ImprimirCommand { get; }

    /// <summary>Abre la cuenta por cobrar que generó este despacho, con la fila ya marcada.</summary>
    public ICommand VerFacturaCommand { get; }

    public string Resumen =>
        $"{Items.Count(d => d.Estado == EstadoDespacho.Registrado)} despachos · {_servicio.DelMes().Count} este mes";

    protected override string ModuloPermiso => "Despachos";

    protected override string Describir(Despacho item) => item.Numero;

    protected override bool CoincideBusqueda(Despacho item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.AutorizadoPorNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.ClienteNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.ProductoNombre.Contains(texto, StringComparison.OrdinalIgnoreCase));

    protected override bool PasaFiltroExtra(Despacho item) => _filtro switch
    {
        "Ventas" => item.Tipo == TipoDespacho.Venta,
        "Ajustes" => item.Tipo == TipoDespacho.Ajuste,
        "Anulados" => item.Estado == EstadoDespacho.Anulado,
        _ => true
    };

    protected override bool PuedeEditar(Despacho item) => false;

    protected override bool PuedeEliminar(Despacho item) => false;

    /// <summary>El alta a mano desde esta pestaña es siempre un Ajuste: una Venta solo se arma
    /// "Despachar pedido" de la pestaña Pedidos, ver <see cref="DespachosService.Validar"/>.</summary>
    protected override Despacho CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Estado = EstadoDespacho.Registrado,
        Tipo = TipoDespacho.Ajuste
    };

    protected override CrudEditorViewModelBase<Despacho> CrearEditor(Despacho item) =>
        new DespachoEditorViewModel(item, TipoDespacho.Ajuste, _productos.ActivosConExistencia(), _clientes,
                                    _productos.LotesConExistencia(), _sesionActual.UsuarioActual?.NombreCompleto ?? string.Empty, _servicio);

    /// <summary>
    /// La emisión pasa por el servicio de dominio: es él quien asigna el número del despacho,
    /// estampa quién autoriza, comprueba que haya existencia suficiente EN VIVO y, si es una
    /// Venta, deja la cuenta por cobrar en Finanzas.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(), _sesionActual.UsuarioActual?.Id ?? 0),
                d => $"Despacho {d.Numero} registrado");
    }

    private void Anular()
    {
        if (SelectedItem is not { } despacho)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular despacho {despacho.Numero}",
            $"{despacho.TipoTexto} — {despacho.TotalCantidad:N2} en total — autorizó {despacho.AutorizadoPorNombre}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(despacho, editor.Motivo),
                d => $"Despacho {d.Numero} anulado");
    }

    /// <summary>
    /// Salta a la cuenta por cobrar que generó este despacho.
    ///
    /// El enlace ya estaba guardado en el documento (<c>Despacho.FacturaClienteId</c>) y la ficha lo
    /// pintaba como texto: se veia el numero de la factura pero llegar a ella era volver al
    /// menu, entrar al submodulo y buscarla a mano.
    /// </summary>
    private void VerFactura()
    {
        if (SelectedItem?.FacturaClienteId is not { } facturaId)
            return;

        if (ModuloCatalogo.BuscarModulo("Finanzas") is { } destino
            && ModuloCatalogo.BuscarSubmodulo("Finanzas.CuentasPorCobrar") is { } seccion)
            SolicitarNavegacion(destino, seccion, facturaId);
    }

    /// <summary>
    /// Imprime la fila seleccionada. El documento no se vuelve a leer de la base: se imprime
    /// EXACTAMENTE lo que está en pantalla, porque el papel que se entrega tiene que
    /// coincidir con lo que vio quien lo emitió.
    /// </summary>
    private void Imprimir()
    {
        if (SelectedItem is not { } documento)
            return;

        if (ImpresionDocumento.Imprimir(DocumentosImprimibles.De(documento)))
            Aviso.Mostrar($"Enviado a la impresora: {documento.Numero}");
    }

    private void VerDetalle()
    {
        if (SelectedItem is not { } despacho)
            return;

        _dialogos.MostrarEditor(new DespachoDetalleViewModel(despacho));
    }

    private void Aplicar(Func<Despacho> transicion, Func<Despacho, string> aviso)
    {
        try
        {
            var resultado = transicion();
            SeleccionarTrasRecargar(resultado.Id);
            Aviso.Mostrar(aviso(resultado));
        }
        catch (InvalidOperationException ex)
        {
            _dialogos.Informar("No se pudo completar la operación", ex.Message);
        }
        catch (Exception ex)
        {
            // Lo que NO es una regla de negocio —la conexión que se cae, la escritura que choca
            // con un índice— subía sin capturar hasta el despachador y cerraba la aplicación a
            // media operación. Va en un catch aparte a propósito: el mensaje de arriba lo redactó
            // el servicio para quien lo lee, este es técnico y no se puede prometer más.
            _dialogos.Informar("No se pudo guardar",
                "La operación no llegó a completarse. " + ex.Message);
        }
    }
}

/// <summary>
/// La ficha de "ver detalle" de un despacho: solo lectura, se abre con doble clic sobre la fila
/// (ver <c>PedidosView.xaml.cs</c>). Expone el <see cref="Despacho"/> completo, mismo criterio
/// que <see cref="ProcesoDetalleViewModel"/>.
/// </summary>
public sealed class DespachoDetalleViewModel : CrudEditorViewModelBase
{
    public DespachoDetalleViewModel(Despacho despacho)
    {
        Despacho = despacho;
    }

    public Despacho Despacho { get; }

    public override string Titulo => $"Despacho {Despacho.Numero}";

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
/// El despacho. "Autoriza" no es un campo: se muestra de solo lectura y lo estampa el servicio
/// con el usuario de la sesión, para que no se pueda escribir otro nombre en el papel.
///
/// El <see cref="Tipo"/> ya NO lo elige el operador en pantalla: queda fijo según quién construye
/// el editor — <see cref="DespachosCrudViewModel"/> siempre pasa <see cref="TipoDespacho.Ajuste"/>,
/// <see cref="PedidosCrudViewModel.DespacharCommand"/> siempre pasa <see cref="TipoDespacho.Venta"/>
/// (vía <see cref="PrecargarDesdePedido"/>). Dejarlo interactivo permitía armar un despacho
/// precargado desde un pedido y cambiarlo a Ajuste en pantalla (perdiendo el <c>PedidoOrigen</c>
/// de las líneas sin avisar), o un Ajuste en blanco cambiado a Venta —que
/// <see cref="DespachosService.Validar"/> rechazaría recién al guardar, con un error tardío.
/// </summary>
public sealed class DespachoEditorViewModel : CrudEditorViewModelBase<Despacho>
{
    private readonly Despacho _original;
    private readonly DespachosService _servicio;

    /// <summary>Lotes con existencia en orden FEFO, leídos al abrir el modal.</summary>
    private readonly IReadOnlyList<LoteProducto> _lotes;

    public DespachoEditorViewModel(Despacho original,
                                   TipoDespacho tipo,
                                   IReadOnlyList<Producto> productos,
                                   IReadOnlyList<Cliente> clientes,
                                   IReadOnlyList<LoteProducto> lotes,
                                   string autorizadoPorNombre,
                                   DespachosService servicio)
    {
        _original = original;
        _servicio = servicio;
        _lotes = lotes;

        Productos = productos;
        Clientes = clientes;
        AutorizadoPorNombre = autorizadoPorNombre;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        Observaciones = original.Observaciones;
        Tipo = tipo;
        ClienteSeleccionado = Clientes.FirstOrDefault(c => c.Id == original.ClienteId);

        Lineas.CollectionChanged += AlCambiarLineas;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaDespachoEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    /// <summary>
    /// Llena el despacho con lo que falta entregar de <paramref name="pedido"/>: el cliente del
    /// pedido, y una línea por cada <see cref="PedidoLinea"/> con <see cref="PedidoLinea.Pendiente"/>
    /// positivo. Usa el <see cref="NuevaLinea"/> privado para que cada línea quede suscrita a
    /// <see cref="LineaDespachoEditorViewModel.Cambio"/> — armarlas a mano desde afuera dejaría los
    /// totales del editor sin refrescar. El <see cref="Tipo"/> ya viene fijado en Venta desde el
    /// constructor, no lo toca este método.
    ///
    /// El producto se fija ANTES que el precio: el precio del catálogo solo se propone si el
    /// campo está vacío (ver <see cref="LineaDespachoEditorViewModel.ProductoSeleccionado"/>), así
    /// que asignarlo después es lo que deja el precio del pedido sin que nada lo pise.
    /// </summary>
    public void PrecargarDesdePedido(Pedido pedido)
    {
        ClienteSeleccionado = Clientes.FirstOrDefault(c => c.Id == pedido.ClienteId);

        Lineas.Clear();

        foreach (var lineaPedido in pedido.Lineas.Where(l => l.Pendiente > 0))
        {
            var linea = NuevaLinea();
            linea.ProductoSeleccionado = Productos.FirstOrDefault(p => p.Id == lineaPedido.ProductoId);
            linea.Cantidad = lineaPedido.Pendiente.ToString("0.####");
            linea.PrecioUnitario = lineaPedido.PrecioUnitario.ToString("0.####");
            linea.PedidoOrigen = pedido;
            Lineas.Add(linea);
        }

        if (Lineas.Count == 0)
            Lineas.Add(NuevaLinea());
    }

    public override string Titulo => Tipo == TipoDespacho.Venta ? "Despachar pedido" : "Registrar ajuste";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => Tipo == TipoDespacho.Venta ? "Despachar pedido" : "Registrar ajuste";

    public IReadOnlyList<Producto> Productos { get; }
    public IReadOnlyList<Cliente> Clientes { get; }

    /// <summary>Quién autoriza: el usuario de la sesión, de solo lectura.</summary>
    public string AutorizadoPorNombre { get; }

    public ObservableCollection<LineaDespachoEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

    // --- Modo: fijo desde la construcción, ver el comentario de cabecera de la clase ---

    public TipoDespacho Tipo { get; }

    public bool EsVenta => Tipo == TipoDespacho.Venta;

    public bool EsAjuste => Tipo == TipoDespacho.Ajuste;

    public string TipoTexto => Tipo == TipoDespacho.Venta ? "Despacho de Venta" : "Despacho de Ajuste";

    public bool MuestraCliente => Tipo == TipoDespacho.Venta;

    /// <summary>En un ajuste no se vende nada, así que no hay precios que pedir.</summary>
    public bool MuestraPrecios => Tipo == TipoDespacho.Venta;

    public string NotaModo => Tipo switch
    {
        TipoDespacho.Venta =>
            "Al registrarlo se creará la cuenta por cobrar en Finanzas · Cuentas por Cobrar.",
        _ => "Un ajuste solo corrige la existencia: no genera ninguna cuenta por cobrar."
    };

    // --- Cabecera ---

    private Cliente? _clienteSeleccionado;
    public Cliente? ClienteSeleccionado
    {
        get => _clienteSeleccionado;
        set => SetProperty(ref _clienteSeleccionado, value);
    }

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
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
        if (Lineas.Any(l => l.ProductoSeleccionado is not null && !l.CantidadEsValida))
        {
            error = "Hay una cantidad que no es un número válido.";
            return false;
        }

        if (MuestraPrecios && Lineas.Any(l => l.ProductoSeleccionado is not null && !l.PrecioEsValido))
        {
            error = "Hay un precio que no es un número válido.";
            return false;
        }

        if (Lineas.Any(l => l.ProductoSeleccionado is not null && l.LoteSeleccionado is null))
        {
            error = "Seleccione el lote de cada producto.";
            return false;
        }

        // La autoridad es el servicio, que vuelve a mirar la existencia real en el momento de
        // guardar: entre que se abrió el despacho y se emite, otro puesto pudo haber despachado.
        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override Despacho ObtenerResultado()
    {
        var despacho = _original.Clonar();
        despacho.Tipo = Tipo;
        despacho.Fecha = Fecha.Date;
        despacho.Observaciones = Observaciones.Trim();

        despacho.ClienteId = Tipo == TipoDespacho.Venta ? ClienteSeleccionado?.Id : null;
        despacho.ClienteNombre = Tipo == TipoDespacho.Venta ? ClienteSeleccionado?.Nombre ?? string.Empty : string.Empty;

        despacho.Lineas = [.. Lineas
            .Where(l => l.ProductoSeleccionado is not null)
            .Select(l => l.Construir(MuestraPrecios))];

        despacho.Total = despacho.Lineas.Sum(l => l.Subtotal);

        return despacho;
    }

    private LineaDespachoEditorViewModel NuevaLinea()
    {
        var linea = new LineaDespachoEditorViewModel(Productos, _lotes);
        linea.Cambio += AlCambiarLinea;
        return linea;
    }

    private void AlCambiarLineas(object? remitente, NotifyCollectionChangedEventArgs e)
    {
        foreach (var linea in e.OldItems?.OfType<LineaDespachoEditorViewModel>() ?? [])
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

/// <summary>
/// Un renglón del despacho mientras se escribe. Como el de las salidas de materia prima, con la
/// existencia disponible a la vista: avisa en el acto de que se está pidiendo de más, sin esperar
/// a que el servicio rechace el despacho entero. Gana precio/subtotal, mismo patrón "vacío no es
/// cero" que <see cref="LineaEntradaEditorViewModel"/>.
/// </summary>
public sealed class LineaDespachoEditorViewModel : ViewModelBase
{
    public event EventHandler? Cambio;

    private readonly IReadOnlyList<LoteProducto> _lotes;

    public LineaDespachoEditorViewModel(IReadOnlyList<Producto> productos, IReadOnlyList<LoteProducto> lotes)
    {
        Productos = productos;
        _lotes = lotes;
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

            // Propone el precio del catálogo solo si el operador todavía no escribió nada en esta
            // línea; si ya había un precio a mano, cambiar de producto no lo pisa.
            if (value is { PrecioUnitario: > 0 } && string.IsNullOrWhiteSpace(PrecioUnitario))
                PrecioUnitario = value.PrecioUnitario.ToString("0.####");

            OnPropertyChanged(nameof(LotesDelProducto));

            // Propone el lote que vence primero; si el lote ya elegido es de otro producto, se
            // reemplaza.
            if (LoteSeleccionado is null || LoteSeleccionado.ProductoId != value?.Id)
                LoteSeleccionado = LotesDelProducto.FirstOrDefault();

            Recalcular();
        }
    }

    /// <summary>Lotes con existencia del producto elegido, en orden FEFO.</summary>
    public IReadOnlyList<LoteProducto> LotesDelProducto =>
        ProductoSeleccionado is null
            ? []
            : [.. _lotes.Where(l => l.ProductoId == ProductoSeleccionado.Id)];

    /// <summary>El lote del que sale esta línea: obligatorio, descuenta su existencia.</summary>
    private LoteProducto? _loteSeleccionado;
    public LoteProducto? LoteSeleccionado
    {
        get => _loteSeleccionado;
        set { if (SetProperty(ref _loteSeleccionado, value)) Recalcular(); }
    }

    /// <summary>El <see cref="Pedido"/> que esta línea cumple, si el despacho se armó con
    /// "Despachar pedido" (ver <see cref="DespachoEditorViewModel.PrecargarDesdePedido"/>). Nulo
    /// en un despacho armado a mano.</summary>
    public Pedido? PedidoOrigen { get; set; }

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

    public decimal Disponible => LoteSeleccionado?.Existencia ?? 0;

    public string DisponibleTexto => ProductoSeleccionado is null ? string.Empty : $"{Disponible:N2}";

    /// <summary>Se pinta en rojo en la grilla; la comprobación de verdad la hace el servicio.</summary>
    public bool SePasa => ProductoSeleccionado is not null && CantidadValor > Disponible;

    /// <summary>Cero en un Ajuste: ahí no se vendió nada — mismo criterio que
    /// <see cref="LineaEntradaEditorViewModel.Construir"/>.</summary>
    public DespachoLinea Construir(bool conPrecios) => new()
    {
        ProductoId = ProductoSeleccionado!.Id,
        ProductoNombre = ProductoSeleccionado.Nombre,
        UnidadMedidaSnapshot = ProductoSeleccionado.UnidadMedida,
        Cantidad = CantidadValor,
        PrecioUnitario = conPrecios ? PrecioValor : 0m,
        Subtotal = conPrecios ? Subtotal : 0m,
        ProcesoProduccionId = LoteSeleccionado?.ProcesoId,
        ProcesoProduccionNumero = LoteSeleccionado?.Numero ?? string.Empty,
        PedidoId = PedidoOrigen?.Id,
        PedidoNumero = PedidoOrigen?.Numero ?? string.Empty
    };

    private void Recalcular()
    {
        OnPropertyChanged(nameof(Disponible));
        OnPropertyChanged(nameof(DisponibleTexto));
        OnPropertyChanged(nameof(SePasa));
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalTexto));

        Cambio?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Procesos · Despachos: la pantalla del submódulo.
///
/// Es un contenedor de un solo hijo —mismo patrón que <see cref="CuentasPorPagarViewModel"/>—
/// y no un <see cref="PantallaCrudViewModel{T, TId}"/> directo, porque lo que hay que armar
/// antes del listado no es una fuente de datos sino una cadena de servicios: un despacho de
/// Venta deja su cuenta por cobrar en Finanzas, y ese servicio a su vez necesita el de Banco.
///
/// <para>Hasta ahora esto vivía como la segunda pestaña de Procesos · Pedidos, y el
/// <c>DespachosCrudViewModel</c> dentro de <c>PedidosViewModel.cs</c> — el único sitio del
/// proyecto donde el ViewModel de un padrón vivía en el archivo de otro. El efecto que no se
/// veía era de permisos: <c>Ver.Procesos.Pedidos</c> era lo único que gateaba el acceso a los
/// despachos, así que quien no viera Pedidos no veía Despachos aunque tuviera
/// <c>Despachos.Crear</c>. Ahora cada uno tiene su clave y se conceden por separado.</para>
///
/// <para>"Despachar pedido" NO se duplica aquí: sigue viviendo en Pedidos
/// (<see cref="PedidosCrudViewModel.DespacharCommand"/>), que es donde se sabe qué falta
/// entregar. Desde esta pantalla solo nace el Ajuste, que no tiene pedido detrás.</para>
/// </summary>
public sealed class DespachosViewModel : PantallaViewModelBase
{
    public DespachosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private DespachosViewModel(Modulo modulo,
                               Submodulo submodulo,
                               IServicioDialogo dialogos,
                               ISesionActual sesion)
        : base(modulo, submodulo)
    {
        var despachosDs = DataSourceFactory.CrearDespachos();
        var productosDs = DataSourceFactory.CrearProductos();
        var procesosDs = DataSourceFactory.CrearProcesosProduccion();
        var clientesDs = DataSourceFactory.CrearClientes();
        var facturasClienteDs = DataSourceFactory.CrearFacturasCliente();

        var productosServicio = new ProductosService(productosDs, procesosDs, despachosDs);

        // Misma cadena de dependencias que arma Cuentas por Cobrar: el despacho necesita al
        // servicio de Finanzas para dejar la cuenta por cobrar cuando es una Venta, y ese
        // servicio necesita al de Banco.
        var banco = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(),
                                           DataSourceFactory.CrearCuentasBancarias(), sesion);
        var cuentasPorCobrar = new CuentasPorCobrarService(facturasClienteDs, banco, sesion);

        var servicio = new DespachosService(despachosDs, productosServicio, facturasClienteDs,
                                            cuentasPorCobrar, sesion);

        Despachos = new DespachosCrudViewModel(despachosDs, productosServicio, clientesDs,
                                               servicio, dialogos, sesion);

        // El padron pide el salto ("ver la cuenta por cobrar"), pero quien tiene la linea
        // directa con el shell es la pantalla: el contenedor solo lo deja pasar.
        Despachos.NavegacionSolicitada += (_, e) => SolicitarNavegacion(e.Modulo, e.Submodulo, e.IdASeleccionar);
    }

    public DespachosCrudViewModel Despachos { get; }

    public override void Recargar() => Despachos.Recargar();

    /// <summary>
    /// Marca la fila que pidio quien nos mando aqui. El contenedor no tiene listado propio: lo
    /// pasa al padron, que si sabe recargar y reseleccionar.
    /// </summary>
    public override void SeleccionarAlAbrir(object id)
    {
        if (id is int clave)
            Despachos.SeleccionarAlAbrir(clave);
    }
}
