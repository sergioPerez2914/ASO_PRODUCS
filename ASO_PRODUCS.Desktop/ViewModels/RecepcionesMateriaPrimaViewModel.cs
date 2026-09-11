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
/// Materia Prima · Recepciones: el historial de lo que entró y el registro de lo nuevo.
///
/// Las filas son documentos: no se editan ni se borran, se anulan. Por eso la pantalla apaga
/// Editar y Eliminar y solo deja "Registrar recepción" y "Anular".
/// </summary>
public sealed class RecepcionesMateriaPrimaViewModel : PantallaCrudViewModel<RecepcionMateriaPrima, int>
{
    private const string FiltroTodas = "Todas";

    private readonly ITipoMateriaPrimaDataSource _tipos;
    private readonly IServicioDialogo _dialogos;
    private readonly ISesionActual _sesionActual;
    private readonly MateriaPrimaService _materiaPrima;
    private readonly RecepcionesMateriaPrimaService _servicio;

    private string _filtro = FiltroTodas;

    public RecepcionesMateriaPrimaViewModel(Modulo modulo, Submodulo submodulo)
        : this(modulo, submodulo, DataSourceFactory.CrearRecepcionesMateriaPrima(),
               new ServicioDialogo(), SesionActual.Instancia)
    {
    }

    private RecepcionesMateriaPrimaViewModel(Modulo modulo,
                                             Submodulo submodulo,
                                             IRecepcionMateriaPrimaDataSource recepciones,
                                             IServicioDialogo dialogos,
                                             ISesionActual sesion)
        : base(modulo, submodulo, recepciones, dialogos, sesion)
    {
        _dialogos = dialogos;
        _sesionActual = sesion;
        _tipos = DataSourceFactory.CrearTiposMateriaPrima();

        _materiaPrima = new MateriaPrimaService(_tipos, recepciones, DataSourceFactory.CrearSalidasMateriaPrima());
        _servicio = new RecepcionesMateriaPrimaService(recepciones, _materiaPrima, sesion);

        CambiarFiltroCommand = new RelayCommand<string>(filtro =>
        {
            _filtro = filtro;
            ItemsView.Refresh();
        });

        AnularCommand = new RelayCommand(Anular,
            () => SelectedItem is { } r && _servicio.PuedeAnular(r)
                  && _sesionActual.Puede(Permisos.RecepcionesMateriaPrima.Anular));
    }

    public ICommand CambiarFiltroCommand { get; }
    public ICommand AnularCommand { get; }

    public string Resumen =>
        $"{Items.Count(r => r.Estado == EstadoRecepcionMateriaPrima.Registrada)} recepciones · " +
        $"{_servicio.DelMes().Count} este mes";

    protected override string ModuloPermiso => "RecepcionesMateriaPrima";

    protected override bool CoincideBusqueda(RecepcionMateriaPrima item, string texto) =>
        item.Numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Referencia.Contains(texto, StringComparison.OrdinalIgnoreCase)
        || item.Lineas.Any(l => l.TipoMateriaPrimaNombre.Contains(texto, StringComparison.OrdinalIgnoreCase));

    protected override bool PasaFiltroExtra(RecepcionMateriaPrima item) => _filtro switch
    {
        "Anuladas" => item.Estado == EstadoRecepcionMateriaPrima.Anulada,
        _ => true
    };

    /// <summary>Un documento no se corrige ni se borra: se anula, y eso deja constancia.</summary>
    protected override bool PuedeEditar(RecepcionMateriaPrima item) => false;

    protected override bool PuedeEliminar(RecepcionMateriaPrima item) => false;

    protected override RecepcionMateriaPrima CrearNuevo() => new()
    {
        Fecha = DateTime.Today,
        Estado = EstadoRecepcionMateriaPrima.Registrada
    };

    protected override CrudEditorViewModelBase<RecepcionMateriaPrima> CrearEditor(RecepcionMateriaPrima item) =>
        new RecepcionMateriaPrimaEditorViewModel(item,
                                                 [.. _tipos.GetAll().Where(t => t.Activo).OrderBy(t => t.Nombre)],
                                                 _servicio);

    /// <summary>
    /// El alta pasa por el servicio de dominio y no por la fuente de datos: registrar la
    /// recepción exige el correlativo y las validaciones de negocio, que el CRUD genérico no
    /// conoce.
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
        if (SelectedItem is not { } recepcion)
            return;

        var editor = new MotivoEditorViewModel(
            $"Anular recepción {recepcion.Numero}",
            $"Referencia {recepcion.Referencia} — {recepcion.TotalCantidad:N2} en total",
            "Motivo de la anulación",
            "Indique el motivo de la anulación.");

        if (!_dialogos.MostrarEditor(editor))
            return;

        Aplicar(() => _servicio.Anular(recepcion, editor.Motivo));
    }

    private void Aplicar(Func<RecepcionMateriaPrima> transicion)
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

/// <summary>El modal de "Registrar recepción": la referencia y lo que trajo.</summary>
public sealed class RecepcionMateriaPrimaEditorViewModel : CrudEditorViewModelBase<RecepcionMateriaPrima>
{
    private readonly RecepcionMateriaPrima _original;
    private readonly RecepcionesMateriaPrimaService _servicio;

    public RecepcionMateriaPrimaEditorViewModel(RecepcionMateriaPrima original,
                                                IReadOnlyList<TipoMateriaPrima> tipos,
                                                RecepcionesMateriaPrimaService servicio)
    {
        _original = original;
        _servicio = servicio;

        Tipos = tipos;

        Fecha = original.Fecha == default ? DateTime.Today : original.Fecha;
        Referencia = original.Referencia;
        Observaciones = original.Observaciones;

        Lineas.CollectionChanged += AlCambiarLineas;

        AgregarLineaCommand = new RelayCommand(() => Lineas.Add(NuevaLinea()));

        QuitarLineaCommand = new RelayCommand<LineaRecepcionMateriaPrimaEditorViewModel>(linea =>
        {
            if (linea is not null)
                Lineas.Remove(linea);
        });

        Lineas.Add(NuevaLinea());
    }

    public override string Titulo => "Registrar recepción";

    /// <summary>Amplio: lleva una grilla de líneas dentro.</summary>
    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => "Registrar recepción";

    public IReadOnlyList<TipoMateriaPrima> Tipos { get; }

    public ObservableCollection<LineaRecepcionMateriaPrimaEditorViewModel> Lineas { get; } = [];

    public ICommand AgregarLineaCommand { get; }
    public ICommand QuitarLineaCommand { get; }

    private DateTime _fecha = DateTime.Today;
    public DateTime Fecha
    {
        get => _fecha;
        set => SetProperty(ref _fecha, value);
    }

    private string _referencia = string.Empty;
    public string Referencia
    {
        get => _referencia;
        set => SetProperty(ref _referencia, value);
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

        // La autoridad es el servicio: aquí solo se le pregunta, para que el mensaje salga en el
        // formulario en vez de en un diálogo de error después de cerrarlo.
        return _servicio.Validar(ObtenerResultado(), out error);
    }

    public override RecepcionMateriaPrima ObtenerResultado()
    {
        var recepcion = _original.Clonar();
        recepcion.Fecha = Fecha.Date;
        recepcion.Referencia = Referencia.Trim();
        recepcion.Observaciones = Observaciones.Trim();

        // Las líneas en blanco no se mandan: una fila vacía al final es lo normal mientras se
        // carga la recepción, y no tiene por qué impedir guardar.
        recepcion.Lineas = [.. Lineas
            .Where(l => l.TipoSeleccionado is not null)
            .Select(l => l.Construir())];

        return recepcion;
    }

    private LineaRecepcionMateriaPrimaEditorViewModel NuevaLinea()
    {
        var linea = new LineaRecepcionMateriaPrimaEditorViewModel(Tipos);
        linea.Cambio += AlCambiarLinea;
        return linea;
    }

    private void AlCambiarLineas(object? remitente, NotifyCollectionChangedEventArgs e)
    {
        foreach (var linea in e.OldItems?.OfType<LineaRecepcionMateriaPrimaEditorViewModel>() ?? [])
            linea.Cambio -= AlCambiarLinea;

        AlCambiarLinea(this, EventArgs.Empty);
    }

    private void AlCambiarLinea(object? remitente, EventArgs e) => OnPropertyChanged(nameof(TotalCantidad));
}

/// <summary>
/// Un renglón de la grilla de líneas mientras se está escribiendo.
///
/// Es un ViewModel y no el modelo <see cref="RecepcionMateriaPrimaLinea"/> porque los modelos no
/// avisan de sus cambios, y aquí hace falta: el total del pie tiene que moverse según se teclea.
/// La cantidad viaja como texto por el mismo motivo que en el resto de los editores: un campo
/// vacío no es un cero.
/// </summary>
public sealed class LineaRecepcionMateriaPrimaEditorViewModel : ViewModelBase
{
    /// <summary>Avisa al editor de que hay que recalcular el total.</summary>
    public event EventHandler? Cambio;

    public LineaRecepcionMateriaPrimaEditorViewModel(IReadOnlyList<TipoMateriaPrima> tipos)
    {
        Tipos = tipos;
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

    public RecepcionMateriaPrimaLinea Construir() => new()
    {
        TipoMateriaPrimaId = TipoSeleccionado!.Id,
        TipoMateriaPrimaNombre = TipoSeleccionado.Nombre,
        UnidadMedidaSnapshot = TipoSeleccionado.UnidadMedida,
        Cantidad = CantidadValor
    };

    private void Recalcular() => Cambio?.Invoke(this, EventArgs.Empty);
}
