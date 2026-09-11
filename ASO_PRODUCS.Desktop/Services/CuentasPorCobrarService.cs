using System;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas de las cuentas por cobrar. Calco de <see cref="CuentasPorPagarService"/> con Cliente en
/// vez de Proveedor y Entrada en vez de Salida: la factura puede nacer a mano (Finanzas ·
/// Cuentas por Cobrar) o de un despacho (<see cref="DespachosService"/>), y de ahí en adelante
/// solo queda cobrarla o anularla.
/// </summary>
public sealed class CuentasPorCobrarService
{
    private readonly IFacturaClienteDataSource _facturas;
    private readonly MovimientosService _banco;
    private readonly ISesionActual _sesion;

    /// <summary>
    /// El <see cref="MovimientosService"/> es obligatorio: cobrar la factura y anotar la entrada
    /// en el libro son la misma operación (ver <see cref="RegistrarCobro"/>).
    /// </summary>
    public CuentasPorCobrarService(IFacturaClienteDataSource facturas, MovimientosService banco, ISesionActual sesion)
    {
        _facturas = facturas;
        _banco = banco;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    public bool PuedeEditar(FacturaCliente f) => f.Estado == EstadoFacturaCliente.Pendiente;

    public bool PuedeEliminar(FacturaCliente f) => f.Estado == EstadoFacturaCliente.Pendiente;

    public bool PuedeRegistrarCobro(FacturaCliente f) => f.Estado == EstadoFacturaCliente.Pendiente;

    public bool PuedeAnular(FacturaCliente f) => f.Estado == EstadoFacturaCliente.Pendiente;

    /// <summary>
    /// Valida antes de guardar. El número de documento no puede repetirse dentro del mismo
    /// cliente: es el control que evita cobrar dos veces la misma factura.
    /// </summary>
    public bool Validar(FacturaCliente factura, out string? error)
    {
        if (factura.ClienteId == 0)
        {
            error = "Seleccione el cliente al que se le factura.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(factura.NumeroDocumento))
        {
            error = "Indique el número de la factura.";
            return false;
        }

        if (factura.Monto <= 0)
        {
            error = "El monto de la factura debe ser mayor que cero.";
            return false;
        }

        if (factura.FechaVencimiento is not { } vencimiento || vencimiento.Date < factura.FechaEmision.Date)
        {
            error = "El vencimiento no puede ser anterior a la fecha de emisión.";
            return false;
        }

        var repetida = _facturas.GetByCliente(factura.ClienteId)
            .Where(f => f.Id != factura.Id && f.Estado != EstadoFacturaCliente.Anulada)
            .Any(f => string.Equals(f.NumeroDocumento.Trim(), factura.NumeroDocumento.Trim(),
                                    StringComparison.OrdinalIgnoreCase));

        if (repetida)
        {
            error = $"El cliente {factura.ClienteNombre} ya tiene registrada la factura " +
                    $"Nº {factura.NumeroDocumento.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Alta de una cuenta por cobrar que NACE DE OTRO MÓDULO (hoy, de un despacho tipo Venta), no
    /// de un formulario de Finanzas.
    ///
    /// Existe para que ese camino pase por la misma validación que el alta manual, en vez de
    /// escribir directo contra la fuente de datos. No vuelve a comprobar el permiso: lo exigió
    /// quien llama, con el permiso de SU documento (<see cref="Permisos.Despachos.Crear"/>). Mismo
    /// criterio que <see cref="CuentasPorPagarService.Crear"/>.
    /// </summary>
    public FacturaCliente Crear(FacturaCliente factura, int usuarioId)
    {
        if (!Validar(factura, out var error))
            throw new InvalidOperationException(error);

        factura.Estado = EstadoFacturaCliente.Pendiente;
        factura.CreadoPorId = usuarioId;
        factura.FechaCreacion = DateTime.Now;
        return _facturas.Add(factura);
    }

    /// <summary>
    /// Da la factura por cobrada y anota la entrada en el libro de banco, en una sola operación.
    /// El asiento va primero porque es el que puede rechazar.
    /// </summary>
    public FacturaCliente RegistrarCobro(FacturaCliente factura, AsientoBanco asiento, int usuarioId)
    {
        if (!PuedeRegistrarCobro(factura))
            throw new InvalidOperationException("Solo se puede cobrar una factura pendiente.");

        if (!_sesion.Puede(Permisos.Finanzas.Cobrar))
            throw new InvalidOperationException("No tienes permiso para registrar cobros de cliente.");

        _banco.RegistrarCobroCliente(factura, asiento, usuarioId);

        var copia = factura.Clonar();
        copia.Estado = EstadoFacturaCliente.Cobrada;
        copia.FechaCobro = DateTime.Today;
        _facturas.Update(copia);
        return copia;
    }

    public FacturaCliente Anular(FacturaCliente factura, string motivo)
    {
        if (!PuedeAnular(factura))
            throw new InvalidOperationException(
                factura.Estado == EstadoFacturaCliente.Cobrada
                    ? "Una factura ya cobrada no se anula: registre la nota de crédito al cliente."
                    : "Solo se puede anular una factura pendiente.");

        if (!_sesion.Puede(Permisos.Finanzas.Anular))
            throw new InvalidOperationException("No tienes permiso para anular facturas de cliente.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var copia = factura.Clonar();
        copia.Estado = EstadoFacturaCliente.Anulada;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _facturas.Update(copia);
        return copia;
    }

    /// <summary>Por cobrar del centro.</summary>
    public decimal TotalPorCobrar() =>
        _facturas.GetAll()
            .Where(f => f.Estado == EstadoFacturaCliente.Pendiente)
            .Sum(f => f.Monto);

    public decimal TotalVencido() =>
        _facturas.GetAll()
            .Where(f => f.EstaVencida)
            .Sum(f => f.Monto);
}
