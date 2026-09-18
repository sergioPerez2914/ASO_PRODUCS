using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// "Transformar": producir una presentación o subproducto (Mantequilla 200 g, Ghee) a partir de
/// un lote de su producto base. El operador elige qué quiere obtener y cuánto; el consumo (lote
/// base + empaque) sale de la receta del producto y se ve en vivo antes de confirmar.
/// </summary>
public sealed class TransformarLoteEditorViewModel : CrudEditorViewModelBase
{
    private readonly TransformacionesService.Existencias _existencias;
    private readonly int? _loteInicialId;

    /// <summary>Abre el formulario y registra la transformación. Devuelve el proceso creado, o
    /// nulo si se canceló, no había nada que transformar o el servicio la rechazó (ya avisado).</summary>
    public static ProcesoProduccion? Abrir(TransformacionesService servicio, IServicioDialogo dialogos,
                                           ISesionActual sesion, LoteProducto? loteInicial)
    {
        var derivados = servicio.ProductosDerivados()
            .Where(p => loteInicial is null || p.ProductoBaseId == loteInicial.ProductoId)
            .ToList();

        if (derivados.Count == 0)
        {
            dialogos.Informar("Nada que transformar",
                (loteInicial is null
                    ? "Todavía no hay ningún producto configurado como presentación o subproducto de otro."
                    : $"Ningún producto está configurado como presentación o subproducto de {loteInicial.ProductoNombre}.") +
                "\n\nEn Procesos · Productos y Lotes, edite el producto que quiere obtener (por ejemplo " +
                "\"Mantequilla 200 g\") y marque \"Se obtiene transformando otro producto\".");
            return null;
        }

        var editor = new TransformarLoteEditorViewModel(servicio, servicio.TomarExistencias(), derivados, loteInicial);

        if (!dialogos.MostrarEditor(editor))
            return null;

        try
        {
            var extra = editor.LineasExtra
                .Where(l => l.TieneMaterialSeleccionado)
                .Select(l => l.ConstruirLineaInicial()!)
                .ToList();

            return servicio.Transformar(editor.ProductoSeleccionado!, editor.UnidadesValor, editor.LoteSeleccionado?.ProcesoId,
                                        editor.TerminarAhora, editor.TerminarAhora ? editor.FechaVencimiento : null,
                                        editor.Observaciones, sesion.UsuarioActual?.Id ?? 0, extra);
        }
        catch (InvalidOperationException ex)
        {
            dialogos.Informar("No se pudo transformar", ex.Message);
            return null;
        }
    }

    private TransformarLoteEditorViewModel(TransformacionesService servicio,
                                           TransformacionesService.Existencias existencias,
                                           IReadOnlyList<Producto> productos,
                                           LoteProducto? loteInicial)
    {
        _existencias = existencias;
        _loteInicialId = loteInicial?.ProcesoId;

        Productos = productos;
        ProductoSeleccionado = productos.Count == 1 ? productos[0] : null;

        TiposMateriaPrima = servicio.TiposMateriaPrima();
        Articulos = servicio.Articulos();

        AgregarLineaExtraCommand = new RelayCommand(() => LineasExtra.Add(NuevaLineaExtra()));

        QuitarLineaExtraCommand = new RelayCommand<LineaConsumoEditorViewModel>(linea =>
        {
            if (linea is not null)
                LineasExtra.Remove(linea);
        });

        LineasExtra.Add(NuevaLineaExtra());
    }

    private LineaConsumoEditorViewModel NuevaLineaExtra() =>
        new(TiposMateriaPrima, Articulos, _existencias.Lotes);

    public override string Titulo => "Transformar producto";

    public override double AnchoEditor => Ancho.Amplio;

    public override string TextoAccion => TerminarAhora ? "Transformar y terminar" : "Iniciar transformación";

    public IReadOnlyList<Producto> Productos { get; }

    private Producto? _productoSeleccionado;
    public Producto? ProductoSeleccionado
    {
        get => _productoSeleccionado;
        set
        {
            if (!SetProperty(ref _productoSeleccionado, value))
                return;

            Lotes = value is null ? [] : TransformacionesService.LotesDisponibles(value, _existencias);
            LoteSeleccionado = Lotes.FirstOrDefault(l => l.ProcesoId == _loteInicialId) ?? Lotes.FirstOrDefault();
            FechaVencimiento = value?.DiasVidaUtil is { } dias ? DateTime.Today.AddDays(dias) : null;

            OnTodasLasPropiedadesCambiaron();
            Recalcular();
        }
    }

    public IReadOnlyList<LoteProducto> Lotes { get; private set; } = [];

    public bool HayLotes => Lotes.Count > 0;

    public bool SinLotes => ProductoSeleccionado is not null && Lotes.Count == 0;

    private LoteProducto? _loteSeleccionado;
    public LoteProducto? LoteSeleccionado
    {
        get => _loteSeleccionado;
        set { if (SetProperty(ref _loteSeleccionado, value)) Recalcular(); }
    }

    private string _unidades = string.Empty;
    public string Unidades
    {
        get => _unidades;
        set { if (SetProperty(ref _unidades, value)) Recalcular(); }
    }

    public decimal UnidadesValor => decimal.TryParse(Unidades, out var v) ? v : 0m;

    public string UnidadProducto => ProductoSeleccionado?.UnidadMedida ?? string.Empty;

    /// <summary>La receta en una frase: "Cada 1 unidad lleva 0,2 kg de Mantequilla + 1 Pote".</summary>
    public string RecetaTexto
    {
        get
        {
            if (ProductoSeleccionado is not { } p)
                return string.Empty;

            var unidadBase = Lotes.FirstOrDefault()?.Unidad ?? string.Empty;
            var partes = new List<string> { $"{p.CantidadBasePorUnidad:0.####} {unidadBase} de {p.ProductoBaseNombre}".Replace("  ", " ") };
            partes.AddRange(p.Componentes.Select(c => $"{c.CantidadPorUnidad:0.####} {c.UnidadMedidaSnapshot} de {c.MaterialNombre}".Replace("  ", " ")));

            return $"Cada 1 {p.UnidadMedida} lleva {string.Join(" + ", partes)}.";
        }
    }

    /// <summary>Por defecto termina en el mismo paso: un fraccionado no tiene etapas. Desmarcado,
    /// el proceso queda En proceso para seguir por etapas (Ghee: clarificado, filtrado…).</summary>
    private bool _terminarAhora = true;
    public bool TerminarAhora
    {
        get => _terminarAhora;
        set
        {
            if (SetProperty(ref _terminarAhora, value))
                OnTodasLasPropiedadesCambiaron();
        }
    }

    private DateTime? _fechaVencimiento;
    public DateTime? FechaVencimiento
    {
        get => _fechaVencimiento;
        set => SetProperty(ref _fechaVencimiento, value);
    }

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
    }

    public ObservableCollection<ConsumoPlaneado> Consumo { get; } = [];

    public bool TieneConsumo => Consumo.Count > 0;

    public bool HayFaltantes => Consumo.Any(c => !c.Alcanza);

    // --- Consumo adicional: merma o consumo no previsto en la receta, mismo mecanismo que la
    // grilla de líneas manuales de "Iniciar proceso" (ver LineaConsumoEditorViewModel). Se funde
    // con la receta en TransformacionesService.Transformar, no aquí: este editor solo junta las
    // filas que el operador cargó.

    public IReadOnlyList<TipoMateriaPrima> TiposMateriaPrima { get; }
    public IReadOnlyList<Articulo> Articulos { get; }

    public ObservableCollection<LineaConsumoEditorViewModel> LineasExtra { get; } = [];

    public ICommand AgregarLineaExtraCommand { get; }
    public ICommand QuitarLineaExtraCommand { get; }

    public string ResumenTexto => ProductoSeleccionado is { } p && UnidadesValor > 0
        ? TerminarAhora
            ? $"Se obtendrán {UnidadesValor:0.####} {p.UnidadMedida} de {p.Nombre} en un lote nuevo, listo para despachar."
            : $"Se consumirá lo de abajo y el proceso de {p.Nombre} quedará En proceso, para seguir por etapas en Producción."
        : string.Empty;

    private void Recalcular()
    {
        Consumo.Clear();

        if (ProductoSeleccionado is { } producto)
            foreach (var consumo in TransformacionesService.Planificar(producto, UnidadesValor, LoteSeleccionado?.ProcesoId, _existencias))
                Consumo.Add(consumo);

        OnPropertyChanged(nameof(TieneConsumo));
        OnPropertyChanged(nameof(HayFaltantes));
        OnPropertyChanged(nameof(ResumenTexto));
    }

    protected override bool Validar(out string? error)
    {
        if (ProductoSeleccionado is null)
        {
            error = "Seleccione qué producto quiere obtener.";
            return false;
        }

        if (UnidadesValor <= 0)
        {
            error = "Indique cuánto va a producir, con un número mayor que cero.";
            return false;
        }

        if (Consumo.FirstOrDefault(c => !c.Alcanza) is { } falta)
        {
            error = $"No alcanza {falta.MaterialNombre}: {falta.DisponibleTexto.ToLowerInvariant()}.";
            return false;
        }

        // La existencia de lo agregado a mano no se pre-valida aquí, igual que en "Iniciar
        // proceso": el servicio la rechaza recién al confirmar (ver Fundir en TransformacionesService).
        if (LineasExtra.Any(l => l.TieneMaterialSeleccionado && !l.CantidadEsValida))
        {
            error = "Hay una cantidad de consumo adicional que no es un número válido.";
            return false;
        }

        if (TerminarAhora && FechaVencimiento is { } vence && vence.Date < DateTime.Today)
        {
            error = "La fecha de vencimiento no puede ser anterior a hoy.";
            return false;
        }

        error = null;
        return true;
    }
}
