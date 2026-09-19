using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas de los despachos de producto terminado.
///
/// El <see cref="CuentasPorCobrarService"/> es obligatorio por constructor, no opcional: despachar
/// a un cliente y deberle esa venta son la misma operación, igual que <see cref="EntradasInventarioService"/>
/// se acopla a <see cref="CuentasPorPagarService"/>. Solo un <see cref="TipoDespacho.Ajuste"/> se
/// salta ese paso, porque no hay a quién cobrarle.
/// </summary>
public sealed class DespachosService
{
    private const string Prefijo = "DES-";

    private readonly IDespachoDataSource _despachos;
    private readonly ProductosService _productos;
    private readonly IFacturaClienteDataSource _facturasCliente;
    private readonly CuentasPorCobrarService _cuentasPorCobrar;
    private readonly ISesionActual _sesion;

    public DespachosService(IDespachoDataSource despachos,
                            ProductosService productos,
                            IFacturaClienteDataSource facturasCliente,
                            CuentasPorCobrarService cuentasPorCobrar,
                            ISesionActual sesion)
    {
        _despachos = despachos;
        _productos = productos;
        _facturasCliente = facturasCliente;
        _cuentasPorCobrar = cuentasPorCobrar;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    /// <summary>
    /// Puro y sin tocar la base: lo llama el <c>CanExecute</c> del botón, que se reevalúa
    /// constantemente. Que la factura generada esté ya cobrada se comprueba dentro de
    /// <see cref="Anular"/>, no aquí.
    /// </summary>
    public bool PuedeAnular(Despacho despacho) => despacho.Estado == EstadoDespacho.Registrado;

    // --- Validación ---

    /// <summary>
    /// Valida el despacho contra la existencia REAL del momento, no contra la que tenía el
    /// formulario al abrirse: entre que se abre el modal y se guarda, otro puesto pudo haber
    /// despachado. La pantalla avisa antes, pero quien manda es esta comprobación.
    /// </summary>
    public bool Validar(Despacho despacho, out string? error)
    {
        if (despacho.Lineas.Count == 0)
        {
            error = "Agregue al menos un producto al despacho.";
            return false;
        }

        if (despacho.Lineas.Any(l => l.ProductoId == 0))
        {
            error = "Hay una línea sin producto seleccionado.";
            return false;
        }

        if (despacho.Lineas.Any(l => l.Cantidad <= 0))
        {
            error = "La cantidad de cada línea debe ser mayor que cero.";
            return false;
        }

        if (despacho.Tipo == TipoDespacho.Venta && despacho.ClienteId is null or 0)
        {
            error = "Seleccione el cliente al que se le despacha.";
            return false;
        }

        if (despacho.Tipo == TipoDespacho.Venta && despacho.Lineas.Any(l => l.PedidoId is null))
        {
            error = "Un despacho de Venta solo puede registrarse despachando un pedido; use \"Despachar pedido\" desde Procesos · Pedidos.";
            return false;
        }

        if (despacho.GeneraCuentaPorCobrar && despacho.Lineas.Any(l => l.PrecioUnitario <= 0))
        {
            error = "Indique el precio de cada producto despachado.";
            return false;
        }

        if (despacho.Lineas.FirstOrDefault(l => l.ProcesoProduccionId is null) is { } sinLote)
        {
            error = $"Indique el lote de {sinLote.ProductoNombre}.";
            return false;
        }

        // Un mismo lote en dos líneas no es un reparto: es el mismo lote contado dos veces, con la
        // misma existencia repetida en los dos renglones y sin forma de ver que se está pasando
        // hasta sumarlos. El editor ya no lo ofrece —el desplegable de cada línea esconde los
        // lotes que tomaron las demás, ver <c>DespachoEditorViewModel.LotesDisponiblesPara</c>—,
        // pero la regla vive aquí, igual que la línea repetida de
        // <see cref="ProcesosProduccionService.ValidarLineas"/>.
        var repetido = despacho.Lineas
            .GroupBy(l => l.ProcesoProduccionId!.Value)
            .FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
        {
            var linea = repetido.First();
            error = $"El lote {linea.ProcesoProduccionNumero} de {linea.ProductoNombre} está en más de una " +
                    "línea; júntelas en una sola o reparta el resto en otro lote.";
            return false;
        }

        var lotes = _productos.Lotes().ToDictionary(l => l.ProcesoId);

        foreach (var linea in despacho.Lineas)
        {
            if (!lotes.TryGetValue(linea.ProcesoProduccionId!.Value, out var lote)
                || lote.ProductoId != linea.ProductoId)
            {
                error = $"El lote elegido para {linea.ProductoNombre} no es un lote terminado de ese producto.";
                return false;
            }

            if (linea.Cantidad > lote.Existencia)
            {
                error = $"El lote {lote.Numero} de {linea.ProductoNombre} solo tiene {lote.ExistenciaTexto} " +
                        $"y se piden {linea.Cantidad:N2}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    // --- Transiciones ---

    /// <summary>
    /// Registra el despacho y, si es una Venta, deja la cuenta por cobrar en Finanzas.
    ///
    /// La factura va PRIMERO, mismo motivo que en <see cref="EntradasInventarioService.Registrar"/>:
    /// es la operación que puede rechazar (número de documento repetido del cliente — aunque aquí
    /// el número lo pone el propio despacho, así que en la práctica solo puede chocar si el mismo
    /// número ya existiera, algo que el índice único de <c>Despacho.Numero</c> ya impide). Si
    /// rechaza, el despacho no llega a escribirse y no queda nada a medias.
    /// </summary>
    public Despacho Registrar(Despacho despacho, int usuarioId)
    {
        if (despacho.Id != 0)
            throw new InvalidOperationException("Este despacho ya está emitido.");

        if (!_sesion.Puede(Permisos.Despachos.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar despachos.");

        if (!Validar(despacho, out var error))
            throw new InvalidOperationException(error);

        despacho.Numero = SiguienteNumero();
        despacho.Estado = EstadoDespacho.Registrado;
        despacho.AutorizadoPorId = usuarioId;
        despacho.AutorizadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        despacho.CreadoPorId = usuarioId;
        despacho.FechaCreacion = DateTime.Now;
        despacho.Total = despacho.Lineas.Sum(l => l.Subtotal);

        FacturaCliente? factura = null;

        if (despacho.GeneraCuentaPorCobrar)
        {
            factura = _cuentasPorCobrar.Crear(ConstruirFactura(despacho), usuarioId);
            despacho.FacturaClienteId = factura.Id;
            despacho.FacturaClienteNumero = factura.NumeroDocumento;
        }

        Despacho guardado;

        try
        {
            guardado = _despachos.Add(despacho);
        }
        catch
        {
            // No hay una transacción que abarque las dos tablas: cada fuente de datos abre su
            // propio contexto. Si el despacho no llegó a guardarse, se retira la factura recién
            // creada para no dejar una cuenta por cobrar sin el despacho que la justifica.
            if (factura is not null)
                _facturasCliente.Delete(factura.Id);

            throw;
        }

        if (factura is not null)
        {
            // El enlace inverso solo se puede completar AHORA: hasta este punto el despacho no
            // tenía Id. Mismo criterio que MovimientosService.Transferir completa ContraparteId
            // en los dos sentidos después de que existen los dos asientos.
            factura.DespachoId = guardado.Id;
            factura.DespachoNumero = guardado.Numero;
            _facturasCliente.Update(factura);
        }

        return guardado;
    }

    /// <summary>
    /// Deshace el despacho y, si generó una cuenta por cobrar, la deshace también.
    ///
    /// Si esa factura ya está cobrada, no se puede anular el despacho: el dinero ya entró al
    /// banco, y deshacerlo es cosa de Movimientos, no del almacén — mismo criterio que
    /// <see cref="EntradasInventarioService.Anular"/> con una factura ya pagada.
    /// </summary>
    public Despacho Anular(Despacho despacho, string motivo)
    {
        if (!PuedeAnular(despacho))
            throw new InvalidOperationException("Solo se puede anular un despacho registrado.");

        if (!_sesion.Puede(Permisos.Despachos.Anular))
            throw new InvalidOperationException("No tienes permiso para anular despachos.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        // La factura va primero: es la que puede rechazar.
        if (despacho.FacturaClienteId is { } facturaId && _facturasCliente.GetById(facturaId) is { } factura)
        {
            if (factura.Estado == EstadoFacturaCliente.Cobrada)
                throw new InvalidOperationException(
                    $"La factura Nº {factura.NumeroDocumento} que generó este despacho ya fue cobrada; " +
                    "deshaga el cobro desde Finanzas · Movimientos antes de anular el despacho.");

            if (factura.Estado == EstadoFacturaCliente.Pendiente)
                _cuentasPorCobrar.Anular(factura, $"Despacho {despacho.Numero} anulado: {motivo.Trim()}");
        }

        var copia = despacho.Clonar();
        copia.Estado = EstadoDespacho.Anulado;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _despachos.Update(copia);
        return copia;
    }

    // --- Piezas internas ---

    /// <summary>
    /// Arma la cuenta por cobrar a partir del despacho. El detalle de la factura sale de las
    /// líneas del documento: es exactamente lo que se vendió y a qué precio. Sin plazo de cobro
    /// propio en el despacho, se fija un vencimiento a 30 días — mismo default que ya usa el alta
    /// manual de una factura de cliente.
    /// </summary>
    private static FacturaCliente ConstruirFactura(Despacho d) => new()
    {
        ClienteId = d.ClienteId!.Value,
        ClienteNombre = d.ClienteNombre,
        NumeroDocumento = d.Numero,
        Descripcion = $"Despacho {d.Numero}",
        FechaEmision = d.Fecha,
        FechaVencimiento = d.Fecha.AddDays(30),
        Monto = d.Lineas.Sum(l => l.Subtotal),
        Lineas = d.Lineas.Select(l => new FacturaClienteLinea
        {
            DestinoTexto = l.ProductoNombre,
            CantidadTexto = l.CantidadTexto,
            PrecioUnitario = l.PrecioUnitario,
            Subtotal = l.Subtotal
        }).ToList()
    };

    /// <summary>
    /// Correlativo interno. Se calcula al registrar, no en el formulario, y el índice único
    /// <c>(OrganizacionId, Numero)</c> es la red por si dos puestos coincidieran.
    /// </summary>
    private string SiguienteNumero()
    {
        var ultimo = _despachos.GetAll()
            .Select(d => d.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    // --- Resúmenes para el panel del módulo ---

    public IReadOnlyList<Despacho> DelMes()
    {
        var desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        return [.. _despachos.GetAll()
            .Where(d => d.Estado == EstadoDespacho.Registrado && d.Fecha.Date >= desde)];
    }
}
