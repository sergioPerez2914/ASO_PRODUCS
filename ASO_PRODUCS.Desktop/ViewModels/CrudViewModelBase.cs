using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Base para pantallas "listado con alta/edición/baja" de un catálogo maestro:
/// selección, filtro de texto y los cuatro comandos CRUD con su gating por permiso.
/// Cada maestro solo aporta los cuatro puntos de extensión de abajo.
/// </summary>
public abstract class CrudViewModelBase<T, TId> : ViewModelBase where T : IEntidad<TId>
{
    private readonly ICrudDataSource<T, TId> _source;
    private readonly IServicioDialogo _dialogo;
    private readonly ISesionActual _sesion;

    public ObservableCollection<T> Items { get; }
    public ICollectionView ItemsView { get; }

    protected CrudViewModelBase(ICrudDataSource<T, TId> source,
                                IServicioDialogo? dialogo = null,
                                ISesionActual? sesion = null)
    {
        _source = source;
        _dialogo = dialogo ?? new ServicioDialogo();
        _sesion = sesion ?? SesionActual.Instancia;

        Items = new ObservableCollection<T>(_source.GetAll());
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = FiltrarItem;

        // El contador se recalcula solo. Escuchar la VISTA y no la colección es lo que hace que
        // funcione igual con el buscador, con los desplegables de filtro y con la recarga del
        // bus: las tres terminan en ItemsView.Refresh(), que emite un Reset por aquí. Si
        // escuchara Items, filtrar no movería el número.
        if (ItemsView is INotifyCollectionChanged vista)
            vista.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Conteo));

        AgregarCommand = new RelayCommand(Agregar, () => _sesion.Puede($"{ModuloPermiso}.Crear"));
        EditarCommand = new RelayCommand(Editar, () => SelectedItem is { } e && PuedeEditar(e) && _sesion.Puede($"{ModuloPermiso}.Editar"));
        EliminarCommand = new RelayCommand(Eliminar, () => SelectedItem is { } b && PuedeEliminar(b) && _sesion.Puede($"{ModuloPermiso}.Eliminar"));
    }

    private T? _selectedItem;
    public T? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set { if (SetProperty(ref _textoBusqueda, value)) ItemsView.Refresh(); }
    }

    /// <summary>
    /// El listado pide ir a otro submodulo, opcionalmente con una fila ya marcada: "ver la
    /// cuenta por pagar de esta entrada", "ver la factura de este despacho".
    ///
    /// Vive aqui y no solo en <see cref="PantallaCrudViewModel{T, TId}"/> porque la mitad de los
    /// padrones no son una pantalla: viven dentro de una conmutable (Movimientos, Produccion,
    /// Pedidos, Productos y Lotes, Cuentas por Pagar/Cobrar) y necesitan que su contenedor
    /// reenvie el aviso hasta el shell.
    /// </summary>
    public event EventHandler<NavegacionEventArgs>? NavegacionSolicitada;

    protected void SolicitarNavegacion(Modulo modulo, Submodulo? submodulo, object? idASeleccionar = null)
        => NavegacionSolicitada?.Invoke(this, new NavegacionEventArgs(modulo, submodulo, idASeleccionar));

    public ICommand AgregarCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand EliminarCommand { get; }

    /// <summary>
    /// Cuántas filas se están viendo y de cuántas.
    ///
    /// Ninguna tabla lo decía, y sin eso un filtro que deja la tabla vacía no se distingue de un
    /// catálogo vacío —el estado vacío saca el mismo texto en los dos casos—, ni se sabe cuánto
    /// se está ocultando cuando sí quedan filas. Sin filtro muestra el total a secas: "300
    /// registros" y no "300 de 300", que no dice nada.
    /// </summary>
    public string Conteo
    {
        get
        {
            var total = Items.Count;
            var visibles = ItemsView.Cast<object>().Count();

            if (total == 0)
                return string.Empty;

            return visibles == total
                ? $"{total} {(total == 1 ? "registro" : "registros")}"
                : $"{visibles} de {total}";
        }
    }

    /// <summary>Prefijo de permiso RBAC del módulo, p. ej. "Empleados" (→ "Empleados.Crear").</summary>
    protected abstract string ModuloPermiso { get; }

    protected abstract bool CoincideBusqueda(T item, string texto);
    protected abstract T CrearNuevo();
    protected abstract CrudEditorViewModelBase<T> CrearEditor(T item);

    /// <summary>
    /// Filtro adicional propio del maestro (p. ej. por categoría), que se combina con el
    /// buscador de texto. Por defecto no filtra nada. Al cambiar el criterio, llamar a
    /// <c>ItemsView.Refresh()</c>.
    /// </summary>
    protected virtual bool PasaFiltroExtra(T item) => true;

    /// <summary>
    /// ¿El elemento admite edición? Los documentos con máquina de estados quedan inmutables
    /// al confirmarse. Por defecto todo es editable (comportamiento de un maestro simple).
    /// </summary>
    protected virtual bool PuedeEditar(T item) => true;

    /// <summary>¿El elemento admite borrado? Ver <see cref="PuedeEditar"/>.</summary>
    protected virtual bool PuedeEliminar(T item) => true;

    /// <summary>
    /// Cómo nombrar la fila cuando hay que hablar de ella: «Se eliminará «Lácteos Pérez»» y no
    /// «se eliminará el registro seleccionado», que es lo que decía la pregunta de borrado y no
    /// permitía darse cuenta de que la fila marcada no era la que uno creía. Lo usa también el
    /// aviso de guardado, para que confirme QUÉ se guardó.
    ///
    /// Nulo por defecto —no todo maestro tiene un nombre obvio— y entonces se cae al texto
    /// genérico.
    /// </summary>
    protected virtual string? Describir(T item) => null;

    /// <summary>
    /// Cómo se llama una fila de este listado en el aviso de guardado: "Proveedor", "Artículo".
    /// Por defecto el nombre del tipo, que es correcto para casi todos (<c>Proveedor</c>,
    /// <c>Cliente</c>, <c>Producto</c>) y solo hace falta redefinir donde el tipo no se llama
    /// como la cosa.
    /// </summary>
    protected virtual string NombreDelTipo => typeof(T).Name;

    /// <summary>
    /// "Guardado: Proveedor «Lácteos Pérez»". Guardar no confirmaba nada en ninguna pantalla que
    /// no fuera Configuración: la única señal era que la tabla se recargaba sola, y con la fila
    /// fuera de pantalla eso no se ve (ver <see cref="Services.Aviso"/>).
    ///
    /// El participio va delante, como etiqueta, y no detrás del nombre: "Factura … guardado"
    /// concuerda mal y arreglarlo pediría declarar el género de cada maestro solo para esto.
    /// </summary>
    protected void Avisar(string participio, T item)
        => Aviso.Mostrar(Describir(item) is { Length: > 0 } nombre
            ? $"{participio}: {NombreDelTipo} «{nombre}»"
            : $"{participio}: {NombreDelTipo}");

    private bool FiltrarItem(object obj)
        => obj is T item
           && (string.IsNullOrWhiteSpace(TextoBusqueda) || CoincideBusqueda(item, TextoBusqueda.Trim()))
           && PasaFiltroExtra(item);

    // Los tres comandos guardan y no tocan la colección: de eso se encarga la recarga que
    // dispara el bus de cambios (ver CambiosDeDatos). Mantener aquí además el alta/reemplazo a
    // mano duplicaría las filas — la fila entraría una vez por Items.Add y otra por la recarga.
    // Lo único que se conserva es a QUIÉN dejar seleccionado después.

    /// <summary>
    /// Da de alta directo contra la fuente. Las pantallas cuyo documento tiene servicio de
    /// dominio lo redefinen para pasar por él: aquí no hay validación de negocio ni se dispara
    /// nada de lo que el alta arrastre en otros módulos.
    /// </summary>
    protected virtual void Agregar()
    {
        var editor = CrearEditor(CrearNuevo());
        if (!_dialogo.MostrarEditor(editor))
            return;

        var agregado = _source.Add(editor.ObtenerResultado());
        _idASeleccionar = agregado.Id;
        Avisar("Guardado", agregado);
    }

    /// <summary>
    /// Guarda la edición directo contra la fuente. Las pantallas cuyo documento tiene servicio de
    /// dominio lo redefinen para pasar por él: aquí no hay validación de estado ni constancia de
    /// lo que cambió.
    /// </summary>
    protected virtual void Editar()
    {
        if (SelectedItem is not { } actual)
            return;

        var editor = CrearEditor(actual);
        if (!_dialogo.MostrarEditor(editor))
            return;

        var actualizado = editor.ObtenerResultado();
        _source.Update(actualizado);
        _idASeleccionar = actualizado.Id;
        Avisar("Guardado", actualizado);
    }

    /// <summary>
    /// Borra la fila tras confirmar. Se redefine cuando borrar arrastra algo más —lo que cuelga
    /// del documento en otras tablas, que aquí no se conoce.
    /// </summary>
    protected virtual void Eliminar()
    {
        if (SelectedItem is not { } actual)
            return;

        var queSeBorra = Describir(actual) is { Length: > 0 } nombre
            ? $"Se eliminará «{nombre}»."
            : "Se eliminará el registro seleccionado.";

        if (!_dialogo.Confirmar("Eliminar", $"{queSeBorra} Esta acción no se puede deshacer.",
                                "Eliminar", destructivo: true))
            return;

        // El borrado va con red: no hay claves foráneas reales (ver BD/DbContext.cs), pero sí
        // índices y restricciones que pueden rechazar, y la conexión puede caerse a media
        // escritura. Sin este try la excepción subía hasta el despachador y cerraba la
        // aplicación; ahora dice qué pasó y la fila se queda donde estaba.
        try
        {
            _source.Delete(actual.Id);
        }
        catch (Exception ex)
        {
            _dialogo.Informar("No se pudo eliminar", ex.Message);
            return;
        }

        _idASeleccionar = null;
        SelectedItem = default;

        Avisar("Eliminado", actual);
    }

    /// <summary>
    /// Id que debe quedar seleccionado tras la próxima recarga. Se guarda como <c>object</c>
    /// porque <c>TId</c> puede ser <c>int</c> o <c>string</c> y hace falta poder decir "ninguno"
    /// en los dos casos.
    /// </summary>
    private object? _idASeleccionar;

    /// <summary>Deja apuntado qué fila reseleccionar; lo usan las pantallas con transiciones.</summary>
    protected void SeleccionarTrasRecargar(TId id) => _idASeleccionar = id;

    /// <summary>
    /// Marca una fila viniendo de fuera: el salto desde otro documento. Refresca ademas de
    /// apuntar, porque el listado ya se leyo en el constructor y <see cref="SeleccionarTrasRecargar"/>
    /// solo deja anotado a quien elegir en la PROXIMA recarga.
    /// </summary>
    public void SeleccionarAlAbrir(TId id)
    {
        SeleccionarTrasRecargar(id);
        Recargar();
    }

    /// <summary>
    /// Relee el listado.
    ///
    /// Conserva la fila seleccionada BUSCÁNDOLA POR ID, no por referencia: la recarga trae
    /// objetos nuevos y la instancia anterior ya no está en la lista. Sin esto, y como ahora la
    /// recarga es automática y no un botón, cada acción dejaría la tabla sin selección y los
    /// comandos que dependen de ella —Editar, Confirmar, Anular— se apagarían solos.
    /// </summary>
    public virtual void Recargar()
    {
        var idBuscado = _idASeleccionar ?? (SelectedItem is { } actual ? (object?)actual.Id : null);
        _idASeleccionar = null;

        Items.Clear();
        foreach (var item in _source.GetAll())
            Items.Add(item);

        // El refresco va ANTES de reseleccionar: re-aplica el filtro y puede mover el elemento
        // actual de la vista, así que hacerlo después dejaría la selección recién puesta a medias.
        ItemsView.Refresh();

        SelectedItem = idBuscado is null
            ? default
            : Items.FirstOrDefault(i => Equals(i.Id, idBuscado));

        // Los resúmenes derivados (totales, contadores) se recalculan solos al reevaluarse
        // todos los bindings; enumerarlos a mano sería una lista que se queda corta.
        OnTodasLasPropiedadesCambiaron();
    }
}
