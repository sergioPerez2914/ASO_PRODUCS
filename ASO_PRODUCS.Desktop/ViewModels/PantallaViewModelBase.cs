using System;
using System.Windows.Input;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Navigation;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Lo que el shell necesita saber de una pantalla: donde esta en el catalogo y como avisar de
/// que el usuario quiere volver. Es una interfaz y no solo una clase base porque las pantallas
/// llegan por dos ramas de herencia distintas —<see cref="PantallaViewModelBase"/> y
/// <see cref="PantallaCrudViewModel{T, TId}"/>—, y <c>MainWindow</c> las enruta a todas por igual.
/// </summary>
/// <summary>
/// Lo que el shell puede poner al día por su cuenta.
///
/// Va aparte de <see cref="IPantalla"/> porque el resumen de módulo
/// (<see cref="ModuloDashboardViewModel"/>) también se recarga y también escucha el bus, pero no
/// es una pantalla de submódulo: no tiene ruta propia ni botón de volver, y obligarlo a declarar
/// esos miembros solo para que se le pueda pedir una recarga sería inventarle un preámbulo que
/// nadie usa.
/// </summary>
public interface IRecargable
{
    /// <summary>
    /// Relee de la base lo que se está mostrando. Lo llama el bus de cambios tras cada
    /// escritura, y también F5. Sustituye a los botones "Actualizar" que había en las barras.
    /// </summary>
    void Recargar();

    /// <summary>
    /// Da de baja la escucha del bus. Lo llama <c>MainWindow</c> al cambiar de sección y al
    /// cerrarse; ver <see cref="Services.SuscripcionACambios"/> para por qué es obligatorio.
    /// </summary>
    void Desconectar();
}

public interface IPantalla : IRecargable
{
    event EventHandler? VolverSolicitado;

    /// <summary>
    /// La pantalla pide ir a OTRA pantalla, opcionalmente con una fila ya marcada.
    ///
    /// Hasta ahora ninguna pantalla podía navegar: solo el lanzador de Inicio y el resumen del
    /// módulo emitían navegación, y <see cref="VolverSolicitado"/> era el único camino de salida
    /// —siempre hacia arriba—. Eso dejaba muertos los enlaces que los documentos ya guardan
    /// entre sí (la factura que generó un despacho, el pedido del que salió, el proceso que
    /// produjo el lote): el dato estaba, pero llegar al otro documento era volver al menú y
    /// buscarlo a mano.
    ///
    /// Lo escucha <c>MainWindow.Conectar</c>, que es el único punto de navegación de la
    /// aplicación; la pantalla no sabe ni quiere saber cómo se enruta.
    /// </summary>
    event EventHandler<NavegacionEventArgs>? NavegacionSolicitada;

    Modulo Modulo { get; }
    Submodulo? Submodulo { get; }
    ICommand VolverCommand { get; }

    /// <summary>
    /// Se lo dice el shell justo después de crearla, cuando el salto venía con destino concreto.
    /// La pantalla decide qué hacer: los listados CRUD lo pasan a
    /// <c>SeleccionarTrasRecargar</c>; las que no tienen fila que marcar lo ignoran.
    /// </summary>
    void SeleccionarAlAbrir(object id);
}

/// <summary>
/// Base de toda pantalla que ocupa el area de contenido del shell: sabe donde esta en el
/// catalogo de modulos y sabe volver al resumen.
///
/// Los 17 ViewModels de pantalla repetian este preambulo palabra por palabra. Tenerlo en un
/// solo sitio tiene un segundo efecto, mas util que las lineas ahorradas: junto con
/// <see cref="IPantalla"/> le da a <c>MainWindow</c> un tipo comun con el que enrutarlas todas
/// desde una tabla, en vez de un switch de catorce casos identicos.
/// </summary>
public abstract class PantallaViewModelBase : ViewModelBase, IPantalla
{
    /// <summary>Se dispara al pedir volver al resumen del modulo; la ventana principal navega.</summary>
    public event EventHandler? VolverSolicitado;

    /// <inheritdoc />
    public event EventHandler<NavegacionEventArgs>? NavegacionSolicitada;

    /// <summary>Atajo para las subclases: pedir ir a otro submodulo, con una fila ya marcada.</summary>
    protected void SolicitarNavegacion(Modulo modulo, Submodulo? submodulo, object? idASeleccionar = null)
        => NavegacionSolicitada?.Invoke(this, new NavegacionEventArgs(modulo, submodulo, idASeleccionar));

    /// <summary>
    /// Por defecto no hace nada: una pantalla que no es un listado —Configuracion, el marcador
    /// de un submodulo sin construir— no tiene fila que marcar. Las conmutables la redefinen
    /// para pasarselo al padron que corresponda.
    /// </summary>
    public virtual void SeleccionarAlAbrir(object id)
    {
    }

    public Modulo Modulo { get; }

    /// <summary>
    /// Null en las secciones fijas que cuelgan del modulo y no de un submodulo
    /// (Administracion y Peticiones).
    /// </summary>
    public Submodulo? Submodulo { get; }

    public ICommand VolverCommand { get; }

    private readonly SuscripcionACambios _suscripcion;

    protected PantallaViewModelBase(Modulo modulo, Submodulo? submodulo = null)
    {
        Modulo = modulo;
        Submodulo = submodulo;

        VolverCommand = new RelayCommand(() => VolverSolicitado?.Invoke(this, EventArgs.Empty));

        // Suscribirse aquí alcanza a la subclase antes de que su constructor termine, y es
        // seguro: el aviso se entrega por la cola del despachador (ver CambiosDeDatos.Publicar),
        // que no corre hasta que se vuelve al bucle de mensajes.
        _suscripcion = new SuscripcionACambios(Recargar);
    }

    /// <summary>
    /// Qué relee esta pantalla. Vacío por defecto: las que no muestran datos —el marcador de
    /// posición de un submódulo sin construir, Configuración— no tienen nada que releer.
    /// </summary>
    public virtual void Recargar()
    {
    }

    public void Desconectar() => _suscripcion.Dispose();
}

/// <summary>
/// Pantalla que ADEMAS es el listado CRUD de un maestro (Tarifas, Repuestos, Liquidaciones…).
///
/// Repite los tres miembros de <see cref="PantallaViewModelBase"/> en vez de heredarlos, y no
/// es un descuido: C# no tiene herencia multiple y estas pantallas ya heredan de
/// <see cref="CrudViewModelBase{T, TId}"/>. La alternativa —mover el preambulo a la base CRUD—
/// se lo colgaria tambien a los siete ViewModels de padron que viven DENTRO de una pantalla
/// conmutable (los padrones de Movimientos, Produccion, Pedidos y Productos y Lotes), que no tienen modulo ninguno.
///
/// Son dos copias de nueve lineas, no diecisiete, y no pueden desalinearse en silencio:
/// <see cref="IPantalla"/> obliga a las dos a exponer lo mismo.
/// </summary>
public abstract class PantallaCrudViewModel<T, TId> : CrudViewModelBase<T, TId>, IPantalla
    where T : IEntidad<TId>
{
    public event EventHandler? VolverSolicitado;

    // NavegacionSolicitada y SolicitarNavegacion se heredan de CrudViewModelBase.

    /// <summary>
    /// Marca la fila que pidio quien nos mando aqui. El listado ya se leyo en el constructor,
    /// asi que hay que refrescar ademas de apuntar: <c>SeleccionarTrasRecargar</c> solo deja
    /// anotado a quien elegir en la PROXIMA recarga.
    /// </summary>
    public void SeleccionarAlAbrir(object id)
    {
        if (id is TId clave)
            SeleccionarAlAbrir(clave);
    }

    public Modulo Modulo { get; }
    public Submodulo? Submodulo { get; }

    public ICommand VolverCommand { get; }

    private readonly SuscripcionACambios _suscripcion;

    protected PantallaCrudViewModel(Modulo modulo,
                                    Submodulo? submodulo,
                                    ICrudDataSource<T, TId> source,
                                    IServicioDialogo? dialogo = null,
                                    ISesionActual? sesion = null)
        : base(source, dialogo, sesion)
    {
        Modulo = modulo;
        Submodulo = submodulo;

        VolverCommand = new RelayCommand(() => VolverSolicitado?.Invoke(this, EventArgs.Empty));

        // El Recargar de CrudViewModelBase ya relee el listado; las pantallas con más de una
        // tabla lo redefinen para releer también las suyas.
        _suscripcion = new SuscripcionACambios(Recargar);
    }

    public void Desconectar() => _suscripcion.Dispose();
}
