using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Configuration;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// El extracto de una cuenta: lo que entró, lo que salió y cómo quedó el saldo. Sub-listado de
/// Finanzas · Movimientos; el encabezado y el conmutador los pone <see cref="MovimientosViewModel"/>.
///
/// La mayoría de las filas no se teclean aquí: bajan solas desde la factura que se cobró, la que
/// se pagó y la liquidación que se pagó. Lo que sí se registra a mano es lo que no tiene
/// documento — la comisión del banco, un retiro, un aporte.
/// </summary>
public sealed class MovimientosBancoCrudViewModel : CrudViewModelBase<MovimientoBanco, int>
{
    private const string FiltroTodos = "Todos";

    private readonly ICuentaBancariaDataSource _cuentas;
    private readonly MovimientosService _servicio;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;

    private string _filtroEstado = FiltroTodos;

    public MovimientosBancoCrudViewModel(IMovimientoBancoDataSource movimientos,
                                         ICuentaBancariaDataSource cuentas,
                                         MovimientosService servicio,
                                         IServicioDialogo dialogos,
                                         ISesionActual sesion)
        : base(movimientos, dialogos, sesion)
    {
        _cuentas = cuentas;
        _servicio = servicio;
        _dialogos = dialogos;
        _sesionActual = sesion;

        Cuentas = new ObservableCollection<CuentaBancaria>(cuentas.GetAll());
        _cuentaSeleccionada = Cuentas.FirstOrDefault(c => c.Activa) ?? Cuentas.FirstOrDefault();

        CambiarFiltroEstadoCommand = new RelayCommand<string>(filtro =>
        {
            _filtroEstado = filtro;
            ItemsView.Refresh();
        });

        ConciliarCommand = new RelayCommand(Conciliar,
            () => SelectedItem is { } m && _servicio.PuedeConciliar(m) && _sesionActual.Puede(Permisos.Movimientos.Conciliar));

        DesconciliarCommand = new RelayCommand(Desconciliar,
            () => SelectedItem is { } m && _servicio.PuedeDesconciliar(m) && _sesionActual.Puede(Permisos.Movimientos.Conciliar));

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } m && _servicio.PuedeAnular(m) && _sesionActual.Puede(Permisos.Movimientos.Anular));

        TransferirCommand = new RelayCommand(Transferir,
            () => _sesionActual.Puede(Permisos.Movimientos.Transferir));

        VerDetalleCommand = new RelayCommand(VerDetalle);

        CalcularSaldoCorrido();
    }

    public ICommand CambiarFiltroEstadoCommand { get; }
    public ICommand ConciliarCommand { get; }
    public ICommand DesconciliarCommand { get; }
    public ICommand AnularCommand { get; }
    public ICommand TransferirCommand { get; }

    /// <summary>Solo la invoca el doble clic de la grilla (ver <c>MovimientosView.xaml.cs</c>),
    /// nunca un botón — por eso no lleva <c>CanExecute</c>: la guarda de "hay algo seleccionado"
    /// vive dentro de <see cref="VerDetalle"/>. Es una consulta de solo lectura, así que funciona
    /// igual sin importar el <see cref="EstadoMovimientoBanco"/> del asiento.</summary>
    public ICommand VerDetalleCommand { get; }

    /// <summary>
    /// Todas las cuentas, incluidas las cerradas: sus movimientos viejos siguen ahí y hay que
    /// poder consultarlos. Elegir cuenta al REGISTRAR sí se limita a las activas.
    /// </summary>
    public ObservableCollection<CuentaBancaria> Cuentas { get; }

    /// <summary>
    /// La cuenta que se está mirando. Va en la pantalla y no en el diálogo, con el mismo criterio
    /// que el frente de <see cref="HorariosViewModel"/>: se trabaja una cuenta seguida, así que
    /// preguntarla en cada alta sería teclear diez veces la misma respuesta. Además acota la
    /// tabla, que es lo que hace que el saldo de la cabecera signifique algo.
    /// </summary>
    private CuentaBancaria? _cuentaSeleccionada;
    public CuentaBancaria? CuentaSeleccionada
    {
        get => _cuentaSeleccionada;
        set
        {
            if (!SetProperty(ref _cuentaSeleccionada, value))
                return;

            CalcularSaldoCorrido();
            ItemsView.Refresh();
            OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool HayCuentas => Cuentas.Count > 0;

    /// <summary>
    /// Acota el extracto a un rango de fechas. Nulo quiere decir "sin límite por ese lado", que es
    /// como arranca: el libro completo. No toca el saldo corrido — ese se calcula sobre toda la
    /// historia de la cuenta, no sobre lo que se ve (ver <see cref="CalcularSaldoCorrido"/>).
    /// </summary>
    private DateTime? _desde;
    public DateTime? Desde
    {
        get => _desde;
        set { if (SetProperty(ref _desde, value)) ItemsView.Refresh(); }
    }

    private DateTime? _hasta;
    public DateTime? Hasta
    {
        get => _hasta;
        set { if (SetProperty(ref _hasta, value)) ItemsView.Refresh(); }
    }

    /// <summary>
    /// La tabla vacía por no haber cuentas y la tabla vacía por no haber movimientos son dos
    /// situaciones distintas, y el mensaje que saca de cada una también: en la primera hay que ir
    /// a crear una cuenta, en la segunda no falta nada.
    /// </summary>
    public string TituloVacio => HayCuentas ? "No hay movimientos en esta cuenta" : "No hay cuentas";

    public string DetalleVacio => HayCuentas
        ? "Los cobros y pagos aparecen solos al registrarlos en sus facturas. Lo que no viene de "
          + "un documento se agrega con «Nuevo movimiento»."
        : "Cree la primera cuenta en la pestaña Cuentas —el banco, la caja chica— con el saldo que "
          + "tiene hoy, y el libro empieza a contar desde ahí.";

    public decimal SaldoLibro =>
        CuentaSeleccionada is { } cuenta ? _servicio.SaldoDeLibro(cuenta.Id) : 0m;

    public decimal SaldoConciliado =>
        CuentaSeleccionada is { } cuenta ? _servicio.SaldoConciliado(cuenta.Id) : 0m;

    /// <summary>
    /// Lo que el libro dice y el banco todavía no confirmó: el cheque girado que nadie cobró, la
    /// transferencia que no ha caído. Cero quiere decir que la cuenta está cuadrada.
    /// </summary>
    public decimal DiferenciaConciliacion => SaldoLibro - SaldoConciliado;

    public string SaldoLibroTexto => SaldoLibro.ToString("N2");
    public string SaldoConciliadoTexto => SaldoConciliado.ToString("N2");
    public string DiferenciaTexto => DiferenciaConciliacion.ToString("N2");

    /// <summary>
    /// "Banco" y no "Movimientos", aunque la pantalla se llame así: el prefijo tiene que coincidir
    /// con el literal de <see cref="Permisos.Movimientos"/>, que sigue siendo "Banco.*" a propósito
    /// (ver el comentario de allá). Con "Movimientos" se pedía "Movimientos.Crear", una cadena que
    /// no existe en <see cref="MatrizPermisos.Todos"/>, y los tres comandos quedaban apagados para
    /// todos los roles —incluido Desarrollador— porque <c>SesionActual.Puede</c> es un
    /// <c>Contains</c> sin escape de superusuario.
    /// </summary>
    protected override string ModuloPermiso => "Banco";

    protected override string Describir(MovimientoBanco item) => item.Concepto;

    protected override string NombreDelTipo => "Movimiento";

    protected override bool CoincideBusqueda(MovimientoBanco item, string texto) =>
        item.Concepto.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Referencia.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.DocumentoTexto.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.CategoriaTexto.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PasaFiltroExtra(MovimientoBanco item)
    {
        if (CuentaSeleccionada is { } cuenta && item.CuentaId != cuenta.Id)
            return false;

        if (Desde is { } desde && item.Fecha.Date < desde.Date)
            return false;

        if (Hasta is { } hasta && item.Fecha.Date > hasta.Date)
            return false;

        return _filtroEstado switch
        {
            "Registrados" => item.Estado == EstadoMovimientoBanco.Registrado,
            "Conciliados" => item.Estado == EstadoMovimientoBanco.Conciliado,
            "Anulados" => item.Estado == EstadoMovimientoBanco.Anulado,
            "Entradas" => item.Tipo == TipoMovimientoBanco.Entrada,
            "Salidas" => item.Tipo == TipoMovimientoBanco.Salida,
            _ => true
        };
    }

    protected override bool PuedeEditar(MovimientoBanco item) => _servicio.PuedeEditar(item);

    protected override bool PuedeEliminar(MovimientoBanco item) => _servicio.PuedeEliminar(item);

    protected override MovimientoBanco CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Tipo = TipoMovimientoBanco.Salida,
        Categoria = CategoriaMovimiento.GastoVario,
        Origen = OrigenMovimiento.Manual,
        Estado = EstadoMovimientoBanco.Registrado,
        CuentaId = CuentaSeleccionada?.Id ?? 0,
        CuentaNombre = CuentaSeleccionada?.Nombre ?? string.Empty,
        CreadoPorId = _sesionActual.UsuarioActual?.Id ?? 0,
        FechaCreacion = DateTime.Now
    };

    protected override CrudEditorViewModelBase<MovimientoBanco> CrearEditor(MovimientoBanco item) =>
        new MovimientoBancoEditorViewModel(item, _servicio.CuentasActivas(), _servicio);

    /// <summary>
    /// El alta pasa por el servicio de dominio, igual que en Entradas, Salidas, Recepciones,
    /// Procesos, Pedidos y Despachos. Este era el único documento con máquina de estados que
    /// seguía escribiendo directo contra la fuente de datos con el <c>Agregar</c> genérico.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.RegistrarManual(editor.ObtenerResultado(),
                                                _sesionActual.UsuarioActual?.Id ?? 0),
                m => $"Movimiento registrado por {m.MontoTexto}");
    }

    /// <summary>La corrección, por el mismo camino que el alta. Ver <see cref="Agregar"/>.</summary>
    protected override void Editar()
    {
        if (SelectedItem is not { } actual)
            return;

        var editor = CrearEditor(actual);

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.EditarManual(editor.ObtenerResultado()),
                _ => "Movimiento actualizado");
    }

    private void Conciliar()
    {
        if (SelectedItem is not { } movimiento)
            return;

        Aplicar(() => _servicio.Conciliar(movimiento, _sesionActual.UsuarioActual?.Id ?? 0),
                _ => "Movimiento conciliado");
    }

    private void Desconciliar()
    {
        if (SelectedItem is not { } movimiento)
            return;

        Aplicar(() => _servicio.Desconciliar(movimiento),
                _ => "Movimiento desconciliado");
    }

    private void Anular()
    {
        if (SelectedItem is not { } movimiento)
            return;

        var aviso = movimiento.ContraparteId is null
            ? string.Empty
            : " Es media transferencia: se anulará también el movimiento de la otra cuenta.";

        var editor = new MotivoEditorViewModel(
            $"Anular movimiento Nº {movimiento.Id}",
            $"{movimiento.FechaTexto} — {movimiento.Concepto} — {movimiento.TipoTexto} {movimiento.MontoTexto}.{aviso}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(movimiento, editor.Motivo),
                _ => "Movimiento anulado");
    }

    private void Transferir()
    {
        var editor = new TransferenciaEditorViewModel(_servicio.CuentasActivas(), CuentaSeleccionada);

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Transferir(
            editor.CuentaOrigen!.Id,
            editor.CuentaDestino!.Id,
            editor.MontoValor,
            editor.Fecha,
            editor.Concepto,
            editor.Referencia,
            _sesionActual.UsuarioActual?.Id ?? 0).Salida,
                m => $"Transferencia registrada por {m.MontoTexto}");
    }

    private void VerDetalle()
    {
        if (SelectedItem is not { } movimiento)
            return;

        _dialogos.MostrarEditor(new MovimientoBancoDetalleViewModel(movimiento));
    }

    /// <summary>
    /// La lista la repuebla la recarga que dispara la escritura del servicio; aquí solo se apunta
    /// qué movimiento dejar seleccionado y se traduce el rechazo de una regla en un aviso.
    /// </summary>
    private void Aplicar(Func<MovimientoBanco> transicion, Func<MovimientoBanco, string> aviso)
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

    /// <summary>
    /// Relee los movimientos y, además, el catálogo de cuentas: dar de alta una cuenta en la otra
    /// pestaña tiene que dejarla disponible aquí sin salir y volver a entrar.
    /// </summary>
    public override void Recargar()
    {
        var cuentaId = CuentaSeleccionada?.Id;

        Cuentas.Clear();
        foreach (var cuenta in _cuentas.GetAll())
            Cuentas.Add(cuenta);

        // Sin notificar, el ComboBox se quedaría apuntando a la instancia vieja —la recarga trae
        // objetos nuevos— y la selección se vería vacía.
        _cuentaSeleccionada = Cuentas.FirstOrDefault(c => c.Id == cuentaId)
                              ?? Cuentas.FirstOrDefault(c => c.Activa)
                              ?? Cuentas.FirstOrDefault();
        OnPropertyChanged(nameof(CuentaSeleccionada));

        base.Recargar();

        CalcularSaldoCorrido();

        // El refresco va DESPUÉS de recalcular y no es opcional: los modelos no implementan
        // INotifyPropertyChanged, así que rellenar SaldoCorrido sobre filas que la tabla ya
        // enlazó no le llega a nadie. Refresh regenera las filas y vuelve a leer la propiedad.
        ItemsView.Refresh();
    }

    /// <summary>
    /// Rellena el saldo corrido de cada asiento de la cuenta mirada.
    ///
    /// Se calcula sobre TODA la historia de la cuenta y en orden cronológico, no sobre lo que se
    /// ve: filtrar por estado o buscar por texto esconde filas, y un saldo que solo sumara las
    /// visibles daría una cifra que no coincide con la del banco.
    /// </summary>
    private void CalcularSaldoCorrido()
    {
        if (CuentaSeleccionada is not { } cuenta)
            return;

        var saldo = cuenta.SaldoInicial;

        foreach (var movimiento in Items.Where(m => m.CuentaId == cuenta.Id)
                                        .OrderBy(m => m.Fecha)
                                        .ThenBy(m => m.Id))
        {
            saldo += movimiento.Efecto;
            movimiento.SaldoCorrido = saldo;
        }
    }
}

/// <summary>
/// La ficha de "ver detalle" de un asiento de banco: solo lectura, se abre con doble clic sobre la
/// fila (ver <c>MovimientosView.xaml.cs</c>). Documento plano, sin líneas: no hay historial que
/// mostrar aparte del encabezado, a diferencia de <see cref="ProcesoDetalleViewModel"/>.
/// </summary>
public sealed class MovimientoBancoDetalleViewModel : CrudEditorViewModelBase
{
    public MovimientoBancoDetalleViewModel(MovimientoBanco movimiento)
    {
        Movimiento = movimiento;
    }

    public MovimientoBanco Movimiento { get; }

    public override string Titulo => $"Movimiento Nº {Movimiento.Id}";

    /// <summary>Estándar: no lleva grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Estandar;

    public override string TextoAccion => "Cerrar";

    public override bool MuestraCancelar => false;

    protected override bool Validar(out string? error)
    {
        error = null;
        return true;
    }
}

/// <summary>
/// El catálogo de cuentas del centro. Sub-listado de Finanzas · Movimientos.
/// </summary>
public sealed class CuentasBancariasCrudViewModel : CrudViewModelBase<CuentaBancaria, int>
{
    private readonly MovimientosService _servicio;
    private readonly IServicioDialogo _dialogos;

    public CuentasBancariasCrudViewModel(ICuentaBancariaDataSource cuentas,
                                         MovimientosService servicio,
                                         IServicioDialogo dialogos,
                                         ISesionActual sesion)
        : base(cuentas, dialogos, sesion)
    {
        _servicio = servicio;
        _dialogos = dialogos;

        RellenarSaldos();
    }

    protected override string ModuloPermiso => "CuentasBancarias";

    protected override string Describir(CuentaBancaria item) => item.Nombre;

    protected override string NombreDelTipo => "Cuenta";

    /// <summary>El disponible de todas las cuentas activas, que es la cifra que importa.</summary>
    public string ResumenDisponible => $"Disponible {_servicio.DisponibleTotal():N2}";

    public override void Recargar()
    {
        base.Recargar();
        RellenarSaldos();

        // Ver la nota de MovimientosBancoCrudViewModel.Recargar: sin Refresh, el saldo recién
        // calculado no llega a la tabla.
        ItemsView.Refresh();
    }

    /// <summary>
    /// Pone en cada fila el saldo de hoy de su cuenta. Va aquí y no en la entidad porque depende
    /// de la tabla de movimientos, que la fila de la cuenta no conoce.
    /// </summary>
    private void RellenarSaldos()
    {
        foreach (var cuenta in Items)
            cuenta.SaldoActual = _servicio.SaldoDeLibro(cuenta.Id);
    }

    protected override bool CoincideBusqueda(CuentaBancaria item, string texto) =>
        item.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Banco.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.NumeroCuenta.Contains(texto, StringComparison.OrdinalIgnoreCase);

    protected override bool PuedeEliminar(CuentaBancaria item) => _servicio.PuedeEliminarCuenta(item);

    protected override CuentaBancaria CrearNuevo() => new()
    {
        FechaApertura = DateTime.Today,
        Moneda = "Bs",
        Activa = true
    };

    protected override CrudEditorViewModelBase<CuentaBancaria> CrearEditor(CuentaBancaria item) =>
        new CuentaBancariaEditorViewModel(item, _servicio);

    /// <summary>
    /// Borrar una cuenta con movimientos dejaría asientos citando algo que ya no existe. El
    /// servicio lo impide; aquí solo se explica por qué, en vez de dejar el botón gris sin decir
    /// nada.
    /// </summary>
    protected override void Eliminar()
    {
        if (SelectedItem is not { } cuenta)
            return;

        if (!_servicio.PuedeEliminarCuenta(cuenta))
        {
            _dialogos.Informar("No se puede eliminar la cuenta",
                $"{cuenta.Nombre} tiene movimientos registrados. Para dejar de usarla, edítela y "
                + "desmarque \"Activa\": deja de ofrecerse al registrar, y su historial se conserva.");
            return;
        }

        base.Eliminar();
    }
}

/// <summary>
/// Finanzas · Movimientos: el libro de entradas y salidas del centro y el catálogo de cuentas, en una
/// pantalla conmutable, con el mismo patrón que Cuentas por Pagar.
///
/// <b>El sistema no se conecta con ningún banco.</b> Es un libro interno: dice cuánto dinero
/// entró y salió por la aplicación, y la marca de conciliado es lo que sirve para cuadrarlo
/// contra el extracto que traiga el banco en papel.
/// </summary>
public sealed class MovimientosViewModel : PantallaViewModelBase
{
    public const string VistaMovimientos = "Movimientos";
    public const string VistaCuentas = "Cuentas";

    public MovimientosViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private MovimientosViewModel(Modulo modulo,
                           Submodulo submodulo,
                           IServicioDialogo dialogos,
                           ISesionActual sesion)
        : base(modulo, submodulo)
    {
        var cuentas = DataSourceFactory.CrearCuentasBancarias();
        var servicio = new MovimientosService(DataSourceFactory.CrearMovimientosBanco(), cuentas, sesion);

        Movimientos = new MovimientosBancoCrudViewModel(
            DataSourceFactory.CrearMovimientosBanco(), cuentas, servicio, dialogos, sesion);

        Cuentas = new CuentasBancariasCrudViewModel(cuentas, servicio, dialogos, sesion);

        CambiarVistaCommand = new RelayCommand<string>(vista => VistaActual = vista);
    }

    public MovimientosBancoCrudViewModel Movimientos { get; }
    public CuentasBancariasCrudViewModel Cuentas { get; }

    /// <summary>
    /// Los dos listados, aunque solo se vea uno: dar de alta una cuenta tiene que dejarla
    /// disponible al conmutar, y un movimiento nuevo cambia el saldo que muestra la otra pestaña.
    /// </summary>
    public override void Recargar()
    {
        Movimientos.Recargar();
        Cuentas.Recargar();
    }

    private string _vistaActual = VistaMovimientos;
    public string VistaActual
    {
        get => _vistaActual;
        set
        {
            if (SetProperty(ref _vistaActual, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    public bool MostrarMovimientos => VistaActual == VistaMovimientos;
    public bool MostrarCuentas => VistaActual == VistaCuentas;

    /// <summary>
    /// Las dos pestañas, enlazadas en DOS VÍAS al <c>IsChecked</c> de su botón, como en
    /// <see cref="CuentasPorPagarViewModel"/>. El setter solo actúa al marcar: al desmarcar ya hay
    /// otro botón del grupo encendiéndose.
    /// </summary>
    public bool EsMovimientos
    {
        get => MostrarMovimientos;
        set { if (value) VistaActual = VistaMovimientos; }
    }

    public bool EsCuentas
    {
        get => MostrarCuentas;
        set { if (value) VistaActual = VistaCuentas; }
    }

    public ICommand CambiarVistaCommand { get; }
}
