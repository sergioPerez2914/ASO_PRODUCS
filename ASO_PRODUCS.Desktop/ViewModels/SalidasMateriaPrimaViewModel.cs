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
/// Materia Prima · Salidas: el historial de lo que salió y el registro de lo nuevo.
///
/// Misma forma que Recepciones: las filas son documentos, así que no se editan ni se borran, se
/// anulan. Anular devuelve la existencia sola, porque el kardex no cuenta lo anulado.
/// </summary>
public sealed class SalidasMateriaPrimaViewModel : PantallaCrudViewModel<SalidaMateriaPrima, int>
{
    private const string FiltroTodos = "Todos";

    private readonly ITipoMateriaPrimaDataSource _tipos;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly MateriaPrimaService _materiaPrima;
    private readonly SalidasMateriaPrimaService _servicio;

    private string _filtro = FiltroTodos;

    public SalidasMateriaPrimaViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearSalidasMateriaPrima(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private SalidasMateriaPrimaViewModel(Modulo modulo,
                                         Submodulo submodulo,
                                         ISalidaMateriaPrimaDataSource salidas,
                                         IServicioDialogo dialogos,
                                         ISesionActual sesion)
        : base(modulo, submodulo, salidas, dialogos, sesion)
    {
        _dialogos = dialogos;
        _sesionActual = sesion;
        _tipos = DataSourceFactory.CrearTiposMateriaPrima();

        _materiaPrima = new MateriaPrimaService(_tipos, DataSourceFactory.CrearRecepcionesMateriaPrima(), salidas);
        _servicio = new SalidasMateriaPrimaService(salidas, _materiaPrima, sesion);

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } s && _servicio.PuedeAnular(s)
                  && _sesionActual.Puede(Permisos.SalidasMateriaPrima.Anular));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    public string Resumen =>
        $"{Items.Count(s => s.Estado == EstadoSalidaMateriaPrima.Registrada)} salidas · " +
        $"{_servicio.DelMes().Count} este mes";

    protected override string ModuloPermiso => "SalidasMateriaPrima";

    protected override bool CoincideBusqueda(SalidaMateriaPrima item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.AutorizadoPorNombre.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.TipoMateriaPrimaNombre.Contains(texto, StringComparison.OrdinalIgnoreCase));

    protected override bool PasaFiltroExtra(SalidaMateriaPrima item) => _filtro switch
    {
        "Anuladas" => item.Estado == EstadoSalidaMateriaPrima.Anulada,
        _ => true
    };

    protected override bool PuedeEditar(SalidaMateriaPrima item) => false;

    protected override bool PuedeEliminar(SalidaMateriaPrima item) => false;

    protected override SalidaMateriaPrima CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Estado = EstadoSalidaMateriaPrima.Registrada
    };

    protected override CrudEditorViewModelBase<SalidaMateriaPrima> CrearEditor(SalidaMateriaPrima item) =>
        new SalidaMateriaPrimaEditorViewModel(item,
                                              [.. _tipos.GetAll().Where(t => t.Activo).OrderBy(t => t.Nombre)],
                                              _materiaPrima.ExistenciasPorTipo(),
                                              _sesionActual.UsuarioActual?.NombreCompleto ?? string.Empty,
                                              _servicio);

    /// <summary>
    /// La emisión pasa por el servicio de dominio: es él quien asigna el número de la salida,
    /// estampa quién autoriza y comprueba que haya existencia suficiente.
    /// </summary>
    protected override void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Registrar(editor.ObtenerResultado(),
                                          _sesionActual.UsuarioActual?.Id ?? 0));
    }

    private void Anular()
    {
        if (SelectedItem is not { } salida)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular salida {salida.Numero}",
            $"{salida.TotalCantidad:N2} en total — autorizó {salida.AutorizadoPorNombre}",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(salida, editor.Motivo));
    }

    private void Aplicar(Func<SalidaMateriaPrima> transicion)
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
/// La salida. "Autoriza" no es un campo: se muestra de solo lectura y lo estampa el servicio con
/// el usuario de la sesión, para que no se pueda escribir otro nombre en el papel.
/// </summary>
public sealed class SalidaMateriaPrimaEditorViewModel : CrudEditorViewModelBase<SalidaMateriaPrima>
{
    private readonly SalidaMateriaPrima _original;
    private readonly IReadOnlyDictionary<int, decimal> _existencias;
    private readonly SalidasMateriaPrimaService _servicio;

    public SalidaMateriaPrimaEditorViewModel(SalidaMateriaPrima original,
                                             IReadOnlyList<TipoMateriaPrima> tipos,
                                             IReadOnlyDictionary<int, decimal> existencias,
                                             string autorizadoPorNombre,
                                             SalidasMateriaPrimaService servicio)
    {
        _original = original;
        _existencias = existencias;
        _servicio = servicio;

        Tipos = tipos;
        AutorizadoPorNombre = autorizadoPorNombre;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        Observaciones = original.Observaciones;

        Lineas.CollectionChanged += AlCambiarLineas;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaSalidaMateriaPrimaEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    public override string Titulo => "Registrar salida";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Registrar salida";

    public IReadOnlyList<TipoMateriaPrima> Tipos { get; }

    /// <summary>Quién autoriza: el usuario de la sesión, de solo lectura.</summary>
    public string AutorizadoPorNombre { get; }

    public ObservableCollection<LineaSalidaMateriaPrimaEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

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

    protected override bool Validar(out string? error)
    {
        if (Lineas.Any(l => l.TipoSeleccionado is not null && !l.CantidadEsValida))
        {
            error = "Hay una cantidad que no es un número válido.";
            return false;
        }

        // La autoridad es el servicio, que vuelve a mirar la existencia real en el momento de
        // guardar: entre que se abrió la salida y se emite, otro puesto pudo haber despachado.
        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override SalidaMateriaPrima ObtenerResultado()
    {
        var salida = _original.Clonar();
        salida.Fecha = Fecha.Date;
        salida.Observaciones = Observaciones.Trim();

        salida.Lineas = [.. Lineas
            .Where(l => l.TipoSeleccionado is not null)
            .Select(l => l.Construir())];

        return salida;
    }

    private LineaSalidaMateriaPrimaEditorViewModel NuevaLinea()
    {
        var linea = new LineaSalidaMateriaPrimaEditorViewModel(Tipos, _existencias);
        linea.Cambio += AlCambiarLinea;
        return linea;
    }

    private void AlCambiarLineas(object? remitente, NotifyCollectionChangedEventArgs e)
    {
        foreach (var linea in e.OldItems?.OfType<LineaSalidaMateriaPrimaEditorViewModel>() ?? [])
            linea.Cambio -= AlCambiarLinea;

        AlCambiarLinea(this, EventArgs.Empty);
    }

    private void AlCambiarLinea(object? remitente, EventArgs e) => OnPropertyChanged(nameof(TotalCantidad));
}

/// <summary>
/// Un renglón de la salida mientras se escribe. Como el de las recepciones, pero con la
/// existencia disponible a la vista: avisa en el acto de que se está pidiendo de más, sin esperar
/// a que el servicio rechace la salida entera.
/// </summary>
public sealed class LineaSalidaMateriaPrimaEditorViewModel : ViewModelBase
{
    public event EventHandler? Cambio;

    private readonly IReadOnlyDictionary<int, decimal> _existencias;

    public LineaSalidaMateriaPrimaEditorViewModel(IReadOnlyList<TipoMateriaPrima> tipos,
                                                  IReadOnlyDictionary<int, decimal> existencias)
    {
        Tipos = tipos;
        _existencias = existencias;
    }

    public IReadOnlyList<TipoMateriaPrima> Tipos { get; }

    private TipoMateriaPrima? _tipoSeleccionado;
    public TipoMateriaPrima? TipoSeleccionado
    {
        get => _tipoSeleccionado;
        set { if (SetProperty(ref _tipoSeleccionado, value)) Recalcular(); }
    }

    private string _cantidad = string.Empty;
    public string Cantidad
    {
        get => _cantidad;
        set { if (SetProperty(ref _cantidad, value)) Recalcular(); }
    }

    public bool CantidadEsValida =>
        !string.IsNullOrWhiteSpace(Cantidad) && decimal.TryParse(Cantidad, out var v) && v > 0;

    public decimal CantidadValor => decimal.TryParse(Cantidad, out var valor) ? valor : 0;

    public decimal Disponible => TipoSeleccionado is { } tipo ? _existencias.GetValueOrDefault(tipo.Id) : 0;

    public string DisponibleTexto => TipoSeleccionado is null ? string.Empty : $"{Disponible:N2}";

    /// <summary>Se pinta en rojo en la grilla; la comprobación de verdad la hace el servicio.</summary>
    public bool SePasa => TipoSeleccionado is not null && CantidadValor > Disponible;

    public SalidaMateriaPrimaLinea Construir() => new()
    {
        TipoMateriaPrimaId = TipoSeleccionado!.Id,
        TipoMateriaPrimaNombre = TipoSeleccionado.Nombre,
        UnidadMedidaSnapshot = TipoSeleccionado.UnidadMedida,
        Cantidad = CantidadValor
    };

    private void Recalcular()
    {
        OnPropertyChanged(nameof(Disponible));
        OnPropertyChanged(nameof(DisponibleTexto));
        OnPropertyChanged(nameof(SePasa));
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}
