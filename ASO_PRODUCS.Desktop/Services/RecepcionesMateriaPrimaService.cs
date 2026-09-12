using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas de las recepciones de materia prima.
///
/// El <see cref="CuentasPorPagarService"/> es obligatorio por constructor, no opcional —mismo
/// criterio que <see cref="EntradasInventarioService"/>—: recibir materia prima comprada y
/// deberla son la misma operación. Solo <see cref="TipoRecepcionMateriaPrima.OtroOrigen"/> (aporte
/// de un socio, cosecha propia, maquila...) se salta ese paso, porque no hay a quién deberle.
/// </summary>
public sealed class RecepcionesMateriaPrimaService
{
    private const string Prefijo = "REC-";

    private readonly IRecepcionMateriaPrimaDataSource _recepciones;
    private readonly IProveedorDataSource _proveedores;
    private readonly IFacturaProveedorDataSource _facturas;
    private readonly MateriaPrimaService _materiaPrima;
    private readonly CuentasPorPagarService _cuentasPorPagar;
    private readonly ISesionActual _sesion;

    public RecepcionesMateriaPrimaService(IRecepcionMateriaPrimaDataSource recepciones,
                                          IProveedorDataSource proveedores,
                                          IFacturaProveedorDataSource facturas,
                                          MateriaPrimaService materiaPrima,
                                          CuentasPorPagarService cuentasPorPagar,
                                          ISesionActual sesion)
    {
        _recepciones = recepciones;
        _proveedores = proveedores;
        _facturas = facturas;
        _materiaPrima = materiaPrima;
        _cuentasPorPagar = cuentasPorPagar;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    public bool PuedeAnular(RecepcionMateriaPrima recepcion) => recepcion.Estado == EstadoRecepcionMateriaPrima.Registrada;

    // --- Validación ---

    public bool Validar(RecepcionMateriaPrima recepcion, out string? error)
    {
        if (recepcion.Lineas.Count == 0)
        {
            error = "Agregue al menos un tipo de materia prima a la recepción.";
            return false;
        }

        if (recepcion.Lineas.Any(l => l.TipoMateriaPrimaId == 0))
        {
            error = "Hay una línea sin tipo de materia prima seleccionado.";
            return false;
        }

        if (recepcion.Lineas.Any(l => l.Cantidad <= 0))
        {
            error = "La cantidad de cada línea debe ser mayor que cero.";
            return false;
        }

        var repetido = recepcion.Lineas
            .GroupBy(l => l.TipoMateriaPrimaId)
            .FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
        {
            error = $"El tipo {repetido.First().TipoMateriaPrimaNombre} está en más de una línea; " +
                    "júntelas en una sola.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(recepcion.Referencia))
        {
            error = "Indique la referencia de la recepción.";
            return false;
        }

        // Anti-duplicado en la app, sin índice único en BD: mismo criterio que
        // CuentasPorPagarService.Validar con el número de documento del proveedor.
        var referenciaBuscada = recepcion.Referencia.Trim();
        var duplicada = _recepciones.GetAll()
            .Any(r => r.Id != recepcion.Id
                     && r.Estado == EstadoRecepcionMateriaPrima.Registrada
                     && string.Equals(r.Referencia.Trim(), referenciaBuscada, StringComparison.OrdinalIgnoreCase));

        if (duplicada)
        {
            error = $"Ya hay una recepción registrada con la referencia {referenciaBuscada}.";
            return false;
        }

        if (recepcion.Tipo == TipoRecepcionMateriaPrima.CompraProveedor && recepcion.ProveedorId is null or 0)
        {
            error = "Seleccione el proveedor al que se le compró.";
            return false;
        }

        if (recepcion.Tipo == TipoRecepcionMateriaPrima.CompraExterna)
        {
            if (string.IsNullOrWhiteSpace(recepcion.ProveedorNombre))
            {
                error = "Indique a quién se le compró.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(recepcion.RecibidoPor))
            {
                error = "Indique quién de la empresa gestionó la compra.";
                return false;
            }
        }

        if (recepcion.GeneraCuentaPorPagar)
        {
            if (string.IsNullOrWhiteSpace(recepcion.NumeroDocumento))
            {
                error = "Indique el número de la factura o del recibo.";
                return false;
            }

            if (recepcion.Lineas.Any(l => l.PrecioUnitario <= 0))
            {
                error = "Indique el precio de cada tipo de materia prima comprado.";
                return false;
            }

            // La cuenta por pagar exige vencimiento; se comprueba aquí para dar el mensaje del
            // formulario que el usuario tiene delante, y no el de Finanzas.
            if (recepcion.FechaVencimiento is not { } vencimiento || vencimiento.Date < recepcion.Fecha.Date)
            {
                error = "El vencimiento no puede ser anterior a la fecha de la compra.";
                return false;
            }
        }

        error = null;
        return true;
    }

    // --- Transiciones ---

    /// <summary>
    /// Registra la recepción y, salvo que sea de otro origen, deja la cuenta por pagar en
    /// Finanzas. La factura va PRIMERO, por el mismo motivo que en
    /// <see cref="EntradasInventarioService.Registrar"/>: es la operación que puede rechazar
    /// (documento repetido del mismo proveedor). Si rechaza, la recepción no llega a escribirse.
    /// </summary>
    public RecepcionMateriaPrima Registrar(RecepcionMateriaPrima recepcion, int usuarioId)
    {
        if (recepcion.Id != 0)
            throw new InvalidOperationException("Esta recepción ya está registrada.");

        if (!_sesion.Puede(Permisos.RecepcionesMateriaPrima.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar recepciones de materia prima.");

        // El proveedor de una compra externa se resuelve antes de validar, porque la validación
        // de la cuenta por pagar necesita saber a quién se le carga. Solo se da de alta si no
        // existía, y en ese caso no puede haber todavía una factura repetida suya.
        if (recepcion.Tipo == TipoRecepcionMateriaPrima.CompraExterna && !string.IsNullOrWhiteSpace(recepcion.ProveedorNombre))
        {
            var proveedor = ResolverProveedorExterno(recepcion.ProveedorNombre);
            recepcion.ProveedorId = proveedor.Id;
            recepcion.ProveedorNombre = proveedor.Nombre;
        }

        if (!Validar(recepcion, out var error))
            throw new InvalidOperationException(error);

        recepcion.Numero = SiguienteNumero();
        recepcion.Estado = EstadoRecepcionMateriaPrima.Registrada;
        recepcion.Total = recepcion.Lineas.Sum(l => l.Subtotal);
        recepcion.CreadoPorId = usuarioId;
        recepcion.CreadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        recepcion.FechaCreacion = DateTime.Now;

        FacturaProveedor? factura = null;

        if (recepcion.GeneraCuentaPorPagar)
        {
            factura = _cuentasPorPagar.Crear(ConstruirFactura(recepcion), usuarioId);
            recepcion.FacturaProveedorId = factura.Id;
            recepcion.FacturaProveedorNumero = factura.NumeroDocumento;
        }

        try
        {
            return _recepciones.Add(recepcion);
        }
        catch
        {
            // No hay una transacción que abarque las dos tablas: cada fuente de datos abre su
            // propio contexto. Si la recepción no llegó a guardarse, se retira la factura recién
            // creada para no dejar una deuda sin la materia prima que la justifica.
            if (factura is not null)
                _facturas.Delete(factura.Id);

            throw;
        }
    }

    /// <summary>
    /// Deshace la recepción y, con ella, la cuenta por pagar que generó.
    ///
    /// Dos cosas pueden impedirlo, y las dos se comprueban antes de escribir nada: que lo que
    /// trajo ya se haya despachado (la existencia quedaría en negativo) y que la factura ya esté
    /// pagada (eso se deshace con una nota de crédito, no desde Materia Prima).
    /// </summary>
    public RecepcionMateriaPrima Anular(RecepcionMateriaPrima recepcion, string motivo)
    {
        if (!PuedeAnular(recepcion))
            throw new InvalidOperationException("Solo se puede anular una recepción registrada.");

        if (!_sesion.Puede(Permisos.RecepcionesMateriaPrima.Anular))
            throw new InvalidOperationException("No tienes permiso para anular recepciones de materia prima.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var existencias = _materiaPrima.ExistenciasPorTipo();

        foreach (var linea in recepcion.Lineas)
        {
            var quedaria = existencias.GetValueOrDefault(linea.TipoMateriaPrimaId) - linea.Cantidad;

            if (quedaria < 0)
                throw new InvalidOperationException(
                    $"No se puede anular: de {linea.TipoMateriaPrimaNombre} ya se despachó lo que " +
                    "trajo esta recepción.");
        }

        // La factura va primero: es la que puede rechazar.
        if (recepcion.FacturaProveedorId is { } facturaId && _facturas.GetById(facturaId) is { } factura)
        {
            if (factura.Estado == EstadoFacturaProveedor.Pagada)
                throw new InvalidOperationException(
                    $"La factura Nº {factura.NumeroDocumento} que generó esta recepción ya fue pagada; " +
                    "registre la nota de crédito del proveedor antes de anular la recepción.");

            if (factura.Estado == EstadoFacturaProveedor.Pendiente)
                _cuentasPorPagar.Anular(factura, $"Recepción {recepcion.Numero} anulada: {motivo.Trim()}");
        }

        var copia = recepcion.Clonar();
        copia.Estado = EstadoRecepcionMateriaPrima.Anulada;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _recepciones.Update(copia);
        return copia;
    }

    // --- Piezas internas ---

    /// <summary>
    /// El proveedor de una compra externa entra al padrón como uno más: la deuda tiene que quedar
    /// a nombre de alguien, y así se paga por el mismo camino de siempre. Se busca por nombre;
    /// solo se da de alta si de verdad no estaba —mismo criterio que
    /// <see cref="EntradasInventarioService"/> con el comercio de una compra externa de almacén.
    /// </summary>
    private Proveedor ResolverProveedorExterno(string nombre)
    {
        var buscado = nombre.Trim();

        var existente = _proveedores.GetAll()
            .FirstOrDefault(p => string.Equals(p.Nombre.Trim(), buscado, StringComparison.OrdinalIgnoreCase));

        if (existente is not null)
            return existente;

        if (!_sesion.Puede(Permisos.Proveedores.Crear))
            throw new InvalidOperationException(
                $"{buscado} no está en el padrón de proveedores y no tienes permiso para darlo de alta.");

        return _proveedores.Add(new Proveedor
        {
            Nombre = buscado,
            Activo = true,
            Notas = "Alta automática desde una compra externa de materia prima."
        });
    }

    /// <summary>
    /// Arma la cuenta por pagar a partir de la recepción. El detalle de la factura sale de las
    /// líneas del documento: es exactamente lo que se compró y a qué precio.
    /// </summary>
    private static FacturaProveedor ConstruirFactura(RecepcionMateriaPrima recepcion) => new()
    {
        ProveedorId = recepcion.ProveedorId!.Value,
        ProveedorNombre = recepcion.ProveedorNombre,
        NumeroDocumento = recepcion.NumeroDocumento.Trim(),
        Descripcion = recepcion.Tipo == TipoRecepcionMateriaPrima.CompraExterna
            ? $"Recepción de materia prima {recepcion.Numero} · compra externa gestionada por {recepcion.RecibidoPor}"
            : $"Recepción de materia prima {recepcion.Numero}",
        FechaEmision = recepcion.Fecha,
        FechaVencimiento = recepcion.FechaVencimiento,
        Monto = recepcion.Lineas.Sum(l => l.Subtotal),
        Lineas = recepcion.Lineas.Select(l => new FacturaProveedorLinea
        {
            DestinoTexto = l.TipoMateriaPrimaNombre,
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
        var ultimo = _recepciones.GetAll()
            .Select(r => r.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    // --- Resúmenes para el panel del módulo ---

    public IReadOnlyList<RecepcionMateriaPrima> DelMes()
    {
        var desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        return [.. _recepciones.GetAll()
            .Where(r => r.Estado == EstadoRecepcionMateriaPrima.Registrada && r.Fecha.Date >= desde)];
    }

    public decimal TotalComprasDelMes() => DelMes().Where(r => r.GeneraCuentaPorPagar).Sum(r => r.Total);
}
