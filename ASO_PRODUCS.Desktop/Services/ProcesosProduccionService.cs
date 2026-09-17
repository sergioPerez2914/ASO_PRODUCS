using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas de los procesos de producción: qué se puede consumir, cómo se generan las salidas de
/// materia prima/inventario que arrastra cada consumo, y la máquina de estados
/// EnProceso → Terminado, con Anulado alcanzable desde cualquiera de los dos.
///
/// Depende de <see cref="SalidasMateriaPrimaService"/> y <see cref="SalidasInventarioService"/>
/// por constructor —no opcional—, mismo patrón que <c>EntradasInventarioService</c> depende de
/// <c>CuentasPorPagarService</c>: recibir materia prima o inventario para fabricar y que ese
/// consumo quede registrado son la misma operación.
/// </summary>
public sealed class ProcesosProduccionService
{
    private const string Prefijo = "PRO-";

    private readonly IProcesoProduccionDataSource _procesos;
    private readonly ProductosService _productos;
    private readonly SalidasMateriaPrimaService _salidasMateriaPrima;
    private readonly SalidasInventarioService _salidasInventario;
    private readonly IArticuloDataSource _articulos;
    private readonly ISesionActual _sesion;

    public ProcesosProduccionService(IProcesoProduccionDataSource procesos,
                                     ProductosService productos,
                                     SalidasMateriaPrimaService salidasMateriaPrima,
                                     SalidasInventarioService salidasInventario,
                                     IArticuloDataSource articulos,
                                     ISesionActual sesion)
    {
        _procesos = procesos;
        _productos = productos;
        _salidasMateriaPrima = salidasMateriaPrima;
        _salidasInventario = salidasInventario;
        _articulos = articulos;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    public bool PuedeAgregarEtapa(ProcesoProduccion proceso) => proceso.Estado == EstadoProcesoProduccion.EnProceso;

    public bool PuedeTerminar(ProcesoProduccion proceso) => proceso.Estado == EstadoProcesoProduccion.EnProceso;

    public bool PuedeAnular(ProcesoProduccion proceso) =>
        proceso.Estado is EstadoProcesoProduccion.EnProceso or EstadoProcesoProduccion.Terminado;

    // --- Validación ---

    public bool Validar(ProcesoProduccion proceso, out string? error)
    {
        if (proceso.ProductoId == 0)
        {
            error = "Seleccione el producto que va a fabricar.";
            return false;
        }

        if (proceso.CantidadPlaneada <= 0)
        {
            error = "La cantidad planeada debe ser mayor que cero.";
            return false;
        }

        // A diferencia de una Recepción/Salida, LineasIniciales SÍ puede estar vacía: un proceso
        // puede no consumir nada al iniciar y consumir todo por etapas.
        return ValidarLineas(proceso.ProductoId, proceso.LineasIniciales.Select(ALinea), out error);
    }

    private bool ValidarLineas(int productoFabricadoId, IEnumerable<LineaConsumo> lineas, out string? error)
    {
        var lista = lineas.ToList();

        if (lista.Any(l => l.MaterialId == 0))
        {
            error = "Hay una línea de consumo sin material seleccionado.";
            return false;
        }

        if (lista.Any(l => l.Cantidad <= 0))
        {
            error = "La cantidad de cada línea de consumo debe ser mayor que cero.";
            return false;
        }

        // El motivo entra en la clave: una línea de Consumo y una de Merma del mismo material,
        // dentro de la misma etapa, son válidas por separado (terminan en documentos distintos),
        // no un duplicado a fundir. El lote también: dos lotes del mismo producto son dos líneas.
        var repetida = lista.GroupBy(l => (l.Origen, l.MaterialId, l.LoteProcesoId, l.Motivo)).FirstOrDefault(g => g.Count() > 1);

        if (repetida is not null)
        {
            error = $"{repetida.First().MaterialNombre} está en más de una línea con el mismo motivo; júntelas en una sola.";
            return false;
        }

        return ValidarConsumoDeLotes(productoFabricadoId, lista, out error);
    }

    /// <summary>Las líneas de producto: que traigan lote, que el lote sea de ese producto, que no
    /// sea el mismo producto que se fabrica, y que al lote le quede EN VIVO lo que se pide.</summary>
    private bool ValidarConsumoDeLotes(int productoFabricadoId, List<LineaConsumo> lineas, out string? error)
    {
        var deProducto = lineas.Where(l => l.Origen == OrigenMaterial.Producto).ToList();

        if (deProducto.Count == 0)
        {
            error = null;
            return true;
        }

        if (deProducto.Any(l => l.LoteProcesoId is null))
        {
            error = "Indique de qué lote sale cada producto que se consume.";
            return false;
        }

        if (deProducto.FirstOrDefault(l => l.MaterialId == productoFabricadoId) is { MaterialId: > 0 } mismo)
        {
            error = $"Un proceso no puede consumir el mismo producto que fabrica ({mismo.MaterialNombre}).";
            return false;
        }

        var lotes = _productos.Lotes().ToDictionary(l => l.ProcesoId);

        foreach (var grupo in deProducto.GroupBy(l => l.LoteProcesoId!.Value))
        {
            var pedido = grupo.Sum(l => l.Cantidad);
            var primera = grupo.First();

            if (!lotes.TryGetValue(grupo.Key, out var lote) || lote.ProductoId != primera.MaterialId)
            {
                error = $"El lote {primera.LoteNumero} de {primera.MaterialNombre} ya no está disponible.";
                return false;
            }

            if (pedido > lote.Existencia)
            {
                error = $"Del lote {lote.Numero} de {lote.ProductoNombre} quedan {lote.ExistenciaTexto}; " +
                        $"no alcanza para {pedido:N2} {lote.Unidad}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    // --- Transiciones ---

    /// <summary>
    /// Inicia el proceso. El proceso se crea ANTES que las salidas de consumo —al revés que
    /// <c>EntradasInventarioService</c> con su factura— porque la salida necesita el
    /// <c>Id</c>/<c>Numero</c> del proceso ya asignados, no por elección. Por eso el consumo se
    /// pre-valida (sin escribir nada) ANTES de guardar el proceso, para fallar rápido; el
    /// try/catch que sigue solo cubre la ventana de carrera residual entre esa pre-validación y
    /// la escritura —mismo límite que el resto del código ya acepta: no hay una transacción que
    /// abarque dos tablas.
    /// </summary>
    public ProcesoProduccion Iniciar(ProcesoProduccion proceso, int usuarioId)
    {
        if (!_sesion.Puede(Permisos.ProcesosProduccion.Crear))
            throw new InvalidOperationException("No tienes permiso para iniciar procesos de producción.");

        if (!Validar(proceso, out var error))
            throw new InvalidOperationException(error);

        var (consumoMateriaPrima, mermaMateriaPrima, consumoInventario, mermaInventario) =
            ConstruirSalidas(proceso.LineasIniciales.Select(ALinea));

        // LineasIniciales siempre mapea a Motivo Consumo (ver ALinea), así que en la práctica
        // mermaMateriaPrima/mermaInventario siempre salen nulos aquí — el consumo inicial nunca
        // genera merma. Se valida igual por si ese mapeo cambiara.
        if (consumoMateriaPrima is not null && !_salidasMateriaPrima.Validar(consumoMateriaPrima, out var errorConsumoMateriaPrima))
            throw new InvalidOperationException(errorConsumoMateriaPrima);

        if (mermaMateriaPrima is not null && !_salidasMateriaPrima.Validar(mermaMateriaPrima, out var errorMermaMateriaPrima))
            throw new InvalidOperationException(errorMermaMateriaPrima);

        if (consumoInventario is not null && !_salidasInventario.Validar(consumoInventario, out var errorConsumoInventario))
            throw new InvalidOperationException(errorConsumoInventario);

        if (mermaInventario is not null && !_salidasInventario.Validar(mermaInventario, out var errorMermaInventario))
            throw new InvalidOperationException(errorMermaInventario);

        proceso.Numero = SiguienteNumero();
        proceso.Estado = EstadoProcesoProduccion.EnProceso;
        proceso.CreadoPorId = usuarioId;
        proceso.CreadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        proceso.FechaCreacion = DateTime.Now;

        var guardado = _procesos.Add(proceso);

        try
        {
            RegistrarConsumo(guardado, consumoMateriaPrima, mermaMateriaPrima, consumoInventario, mermaInventario,
                usuarioId, $"el consumo inicial del proceso {guardado.Numero}");
        }
        catch
        {
            _procesos.Delete(guardado.Id);
            throw;
        }

        return guardado;
    }

    /// <summary>
    /// Pasa el proceso a la etapa <paramref name="nuevaEtapa"/>. Si ya había una etapa en curso
    /// (<see cref="ProcesoProduccion.EtapaActualId"/> no nulo), primero la cierra con
    /// <paramref name="cierre"/> —confirma cómo salió, descuenta su consumo/merma— y recién
    /// entonces el proceso pasa a la nueva; si no había ninguna en curso todavía (la primera
    /// etapa del proceso), <paramref name="cierre"/> se ignora por completo, porque no hay nada
    /// que cerrar. Nunca se pregunta "cómo salió" la etapa nueva: recién empieza.
    /// </summary>
    public ProcesoProduccion AgregarEtapa(ProcesoProduccion proceso, EtapaProcesoProduccion cierre, EtapaProduccion nuevaEtapa, int usuarioId)
    {
        if (!PuedeAgregarEtapa(proceso))
            throw new InvalidOperationException("Solo se pueden agregar etapas a un proceso en curso.");

        if (!_sesion.Puede(Permisos.ProcesosProduccion.AgregarEtapa))
            throw new InvalidOperationException("No tienes permiso para agregar etapas a un proceso de producción.");

        if (nuevaEtapa.Id == 0)
            throw new InvalidOperationException("Seleccione la etapa a la que pasa el proceso.");

        var copia = proceso.Clonar();
        CerrarEtapaEnCurso(proceso, copia, cierre, usuarioId);

        copia.EtapaActualId = nuevaEtapa.Id;
        copia.EtapaActualNombre = nuevaEtapa.Nombre;

        _procesos.Update(copia);
        return copia;
    }

    /// <summary>
    /// Cierra el proceso con lo que de verdad se obtuvo, que puede diferir de
    /// <see cref="ProcesoProduccion.CantidadPlaneada"/> por una merma —eso no se valida contra la
    /// planeada, es exactamente el dato que interesa registrar. Si había una etapa en curso, antes
    /// se cierra igual que en <see cref="AgregarEtapa"/> —mismo <paramref name="cierre"/>, mismo
    /// motivo: es la última confirmación antes de dejar de fabricar—; si no había ninguna en
    /// curso, <paramref name="cierre"/> se ignora.
    /// </summary>
    public ProcesoProduccion Terminar(ProcesoProduccion proceso, decimal cantidadProducida, DateTime? fechaVencimiento,
                                      EtapaProcesoProduccion cierre, int usuarioId)
    {
        if (!PuedeTerminar(proceso))
            throw new InvalidOperationException("Solo se puede terminar un proceso en curso.");

        if (!_sesion.Puede(Permisos.ProcesosProduccion.Terminar))
            throw new InvalidOperationException("No tienes permiso para terminar procesos de producción.");

        if (cantidadProducida <= 0)
            throw new InvalidOperationException("Indique cuánto se produjo.");

        if (fechaVencimiento is { } vence && vence.Date < DateTime.Today)
            throw new InvalidOperationException("La fecha de vencimiento no puede ser anterior a hoy.");

        var copia = proceso.Clonar();
        CerrarEtapaEnCurso(proceso, copia, cierre, usuarioId);

        copia.CantidadProducida = cantidadProducida;
        copia.FechaVencimiento = fechaVencimiento?.Date;
        copia.Estado = EstadoProcesoProduccion.Terminado;
        copia.TerminadoPorId = usuarioId;
        copia.TerminadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        copia.FechaTermino = DateTime.Now;
        copia.EtapaActualId = null;
        copia.EtapaActualNombre = string.Empty;

        _procesos.Update(copia);
        return copia;
    }

    /// <summary>
    /// Confirma y cierra la etapa en curso de <paramref name="proceso"/> (si hay alguna),
    /// agregándola a <paramref name="copia"/>.Etapas y descontando su consumo/merma —compartido
    /// por <see cref="AgregarEtapa"/> y <see cref="Terminar"/>, que son los dos únicos momentos en
    /// que una etapa en curso se cierra. Si <see cref="ProcesoProduccion.EtapaActualId"/> es nulo
    /// no hay nada que cerrar (el proceso todavía no entró a ninguna etapa) y no hace nada, ni
    /// siquiera mira <paramref name="cierre"/>.
    /// </summary>
    private void CerrarEtapaEnCurso(ProcesoProduccion proceso, ProcesoProduccion copia, EtapaProcesoProduccion cierre, int usuarioId)
    {
        if (proceso.EtapaActualId is null)
            return;

        if (!ValidarLineas(proceso.ProductoId, cierre.Lineas.Select(ALinea), out var error))
            throw new InvalidOperationException(error);

        var (consumoMateriaPrima, mermaMateriaPrima, consumoInventario, mermaInventario) =
            ConstruirSalidas(cierre.Lineas.Select(ALinea));

        RegistrarConsumo(proceso, consumoMateriaPrima, mermaMateriaPrima, consumoInventario, mermaInventario,
            usuarioId, $"la etapa {proceso.EtapaActualNombre} del proceso {proceso.Numero}");

        cierre.EtapaProduccionId = proceso.EtapaActualId.Value;
        cierre.EtapaProduccionNombre = proceso.EtapaActualNombre;
        cierre.FechaRegistro = DateTime.Now;
        copia.Etapas.Add(cierre);
    }

    /// <summary>
    /// Anula el proceso, en curso o ya terminado —tolerante a mermas: un defecto de calidad
    /// detectado después de terminar es un caso válido y esperado, no una excepción rara. Si ya
    /// estaba Terminado, comprueba EN VIVO que anular no deje el producto en negativo (si ya se
    /// despachó lo que produjo).
    ///
    /// Las salidas de materia prima/inventario que generó este proceso NO se revierten NUNCA,
    /// haya llegado a Terminado o no: representan material que físicamente salió del almacén, y
    /// revertirlas automáticamente mentiría sobre el inventario real. Si hace falta devolver
    /// material no usado, es una Entrada/Recepción de tipo Ajuste nueva, igual que ya resuelve
    /// Inventario hoy.
    /// </summary>
    public ProcesoProduccion Anular(ProcesoProduccion proceso, string motivo)
    {
        if (!PuedeAnular(proceso))
            throw new InvalidOperationException("Solo se puede anular un proceso en curso o terminado.");

        if (!_sesion.Puede(Permisos.ProcesosProduccion.Anular))
            throw new InvalidOperationException("No tienes permiso para anular procesos de producción.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        if (proceso.Estado == EstadoProcesoProduccion.Terminado
            && _productos.Lotes().FirstOrDefault(l => l.ProcesoId == proceso.Id) is { } lote)
        {
            if (lote.Despachado > 0)
                throw new InvalidOperationException(
                    $"No se puede anular: ya se despacharon {lote.DespachadoTexto} del lote {proceso.Numero}.");

            if (lote.Transformado > 0)
                throw new InvalidOperationException(
                    $"No se puede anular: ya se usaron {lote.TransformadoTexto} del lote {proceso.Numero} en otra " +
                    "transformación. Anule primero ese proceso.");
        }

        var copia = proceso.Clonar();
        copia.Estado = EstadoProcesoProduccion.Anulado;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _procesos.Update(copia);
        return copia;
    }

    // --- Piezas internas ---

    private readonly record struct LineaConsumo(
        OrigenMaterial Origen, int MaterialId, string MaterialNombre, string UnidadMedidaSnapshot,
        decimal Cantidad, MotivoConsumoEtapa Motivo, int? LoteProcesoId, string? LoteNumero);

    /// <summary>El consumo inicial (antes de la primera etapa) siempre es Consumo normal, nunca
    /// merma: el motivo no se lee del modelo —que no lo tiene— sino que se fija aquí a propósito.
    /// Las líneas de producto no generan salida: su consumo queda en la propia línea del proceso
    /// y lo descuenta <see cref="ProductosService"/>.</summary>
    private static LineaConsumo ALinea(ProcesoProduccionLineaInicial l) =>
        new(l.Origen, l.MaterialId, l.MaterialNombre, l.UnidadMedidaSnapshot, l.Cantidad, MotivoConsumoEtapa.Consumo,
            l.LoteProcesoId, l.LoteNumero);

    private static LineaConsumo ALinea(EtapaProcesoProduccionLinea l) =>
        new(l.Origen, l.MaterialId, l.MaterialNombre, l.UnidadMedidaSnapshot, l.Cantidad, l.Motivo,
            l.LoteProcesoId, l.LoteNumero);

    /// <summary>
    /// Arma, sin escribir nada, hasta cuatro salidas —materia prima y/o inventario, cada una de
    /// Consumo y/o de Merma— que hacen falta para cubrir un grupo de líneas de consumo, agrupando
    /// por <see cref="OrigenMaterial"/> y por <see cref="MotivoConsumoEtapa"/> (un documento solo
    /// puede tener un motivo para todas sus líneas). Cualquiera de las cuatro puede salir nula si
    /// no hubo líneas de esa combinación.
    /// </summary>
    private (SalidaMateriaPrima? ConsumoMateriaPrima, SalidaMateriaPrima? MermaMateriaPrima,
             SalidaInventario? ConsumoInventario, SalidaInventario? MermaInventario)
        ConstruirSalidas(IEnumerable<LineaConsumo> lineas)
    {
        var lista = lineas.ToList();

        var consumoMateriaPrima = ConstruirSalidaMateriaPrima(lista, MotivoConsumoEtapa.Consumo, MotivoSalidaMateriaPrima.Consumo);
        var mermaMateriaPrima = ConstruirSalidaMateriaPrima(lista, MotivoConsumoEtapa.Merma, MotivoSalidaMateriaPrima.Merma);
        var consumoInventario = ConstruirSalidaInventario(lista, MotivoConsumoEtapa.Consumo, MotivoSalida.Consumo);
        var mermaInventario = ConstruirSalidaInventario(lista, MotivoConsumoEtapa.Merma, MotivoSalida.Merma);

        return (consumoMateriaPrima, mermaMateriaPrima, consumoInventario, mermaInventario);
    }

    private static SalidaMateriaPrima? ConstruirSalidaMateriaPrima(
        List<LineaConsumo> lineas, MotivoConsumoEtapa motivoLinea, MotivoSalidaMateriaPrima motivoSalida)
    {
        var filtradas = lineas.Where(l => l.Origen == OrigenMaterial.MateriaPrima && l.Motivo == motivoLinea).ToList();

        if (filtradas.Count == 0)
            return null;

        return new SalidaMateriaPrima
        {
            Fecha = DateTime.Today,
            Motivo = motivoSalida,
            Lineas = filtradas.Select(l => new SalidaMateriaPrimaLinea
            {
                TipoMateriaPrimaId = l.MaterialId,
                TipoMateriaPrimaNombre = l.MaterialNombre,
                UnidadMedidaSnapshot = l.UnidadMedidaSnapshot,
                Cantidad = l.Cantidad
            }).ToList()
        };
    }

    private SalidaInventario? ConstruirSalidaInventario(
        List<LineaConsumo> lineas, MotivoConsumoEtapa motivoLinea, MotivoSalida motivoSalida)
    {
        var filtradas = lineas.Where(l => l.Origen == OrigenMaterial.Articulo && l.Motivo == motivoLinea).ToList();

        if (filtradas.Count == 0)
            return null;

        return new SalidaInventario
        {
            Fecha = DateTime.Today,
            Motivo = motivoSalida,
            RetiradoPor = "Producción",
            Lineas = filtradas.Select(l => new SalidaInventarioLinea
            {
                ArticuloId = l.MaterialId,
                ArticuloCodigo = _articulos.GetById(l.MaterialId)?.Codigo ?? string.Empty,
                ArticuloNombre = l.MaterialNombre,
                UnidadTexto = l.UnidadMedidaSnapshot,
                Cantidad = l.Cantidad
            }).ToList()
        };
    }

    /// <summary>Enlaza las salidas ya armadas al proceso y las emite, sin repetir el permiso de
    /// Materia Prima/Inventario —ver <c>SalidasMateriaPrimaService.RegistrarSinPermiso</c>. Las de
    /// Merma llevan una observación distinta de las de Consumo, para que el documento diga por qué
    /// salió sin tener que abrir el proceso que lo originó.</summary>
    private void RegistrarConsumo(ProcesoProduccion proceso,
        SalidaMateriaPrima? consumoMateriaPrima, SalidaMateriaPrima? mermaMateriaPrima,
        SalidaInventario? consumoInventario, SalidaInventario? mermaInventario,
        int usuarioId, string contexto)
    {
        RegistrarSalidaMateriaPrima(proceso, consumoMateriaPrima, usuarioId, $"Consumo de {contexto}.");
        RegistrarSalidaMateriaPrima(proceso, mermaMateriaPrima, usuarioId, $"Merma/exceso registrado en {contexto}.");
        RegistrarSalidaInventario(proceso, consumoInventario, usuarioId, $"Consumo de {contexto}.");
        RegistrarSalidaInventario(proceso, mermaInventario, usuarioId, $"Merma/exceso registrado en {contexto}.");
    }

    private void RegistrarSalidaMateriaPrima(ProcesoProduccion proceso, SalidaMateriaPrima? salida, int usuarioId, string observaciones)
    {
        if (salida is null)
            return;

        salida.Observaciones = observaciones;
        salida.ProcesoProduccionId = proceso.Id;
        salida.ProcesoProduccionNumero = proceso.Numero;
        _salidasMateriaPrima.RegistrarSinPermiso(salida, usuarioId);
    }

    private void RegistrarSalidaInventario(ProcesoProduccion proceso, SalidaInventario? salida, int usuarioId, string observaciones)
    {
        if (salida is null)
            return;

        salida.Observaciones = observaciones;
        salida.ProcesoProduccionId = proceso.Id;
        salida.ProcesoProduccionNumero = proceso.Numero;
        _salidasInventario.RegistrarSinPermiso(salida, usuarioId);
    }

    /// <summary>
    /// Correlativo interno. Se calcula al iniciar, no en el formulario, y el índice único
    /// <c>(OrganizacionId, Numero)</c> es la red por si dos puestos coincidieran.
    /// </summary>
    private string SiguienteNumero()
    {
        var ultimo = _procesos.GetAll()
            .Select(p => p.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    // --- Resúmenes para el panel del módulo ---

    public IReadOnlyList<ProcesoProduccion> DelMes()
    {
        var desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        return [.. _procesos.GetAll()
            .Where(p => p.Estado != EstadoProcesoProduccion.Anulado && p.Fecha.Date >= desde)];
    }

    public int EnProcesoCount() => _procesos.GetAll().Count(p => p.Estado == EstadoProcesoProduccion.EnProceso);
}
