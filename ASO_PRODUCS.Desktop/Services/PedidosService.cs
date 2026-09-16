using System;
using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas de los pedidos de clientes. Un pedido NO reserva existencia: es solo lo que falta
/// entregar, y se cumple despachándolo (ver <c>DespachoEditorViewModel.PrecargarDesdePedido</c>).
///
/// Cuánto lleva despachado cada línea se DERIVA de <see cref="IDespachoDataSource"/> — no se
/// guarda en el pedido — mismo criterio que las existencias derivadas del resto del código.
/// </summary>
public sealed class PedidosService
{
    private const string Prefijo = "PED-";

    private readonly IPedidoDataSource _pedidos;
    private readonly IDespachoDataSource _despachos;
    private readonly ISesionActual _sesion;

    public PedidosService(IPedidoDataSource pedidos, IDespachoDataSource despachos, ISesionActual sesion)
    {
        _pedidos = pedidos;
        _despachos = despachos;
        _sesion = sesion;
    }

    // --- Reglas de transición (alimentan el CanExecute) ---

    public bool PuedeAnular(Pedido pedido) => pedido.Estado == EstadoPedido.Registrado;

    /// <summary>Solo mientras quede algo pendiente por entregar y el pedido siga vigente. Requiere
    /// <see cref="RellenarDespachado"/> corrido antes, igual que <see cref="PuedeAnular"/> no lo
    /// necesita pero éste sí.</summary>
    public bool PuedeDespachar(Pedido pedido) =>
        pedido.Estado == EstadoPedido.Registrado && pedido.EstadoEntregaTexto != "Completado";

    // --- Validación ---

    public bool Validar(Pedido pedido, out string? error)
    {
        if (pedido.Lineas.Count == 0)
        {
            error = "Agregue al menos un producto al pedido.";
            return false;
        }

        if (pedido.Lineas.Any(l => l.ProductoId == 0))
        {
            error = "Hay una línea sin producto seleccionado.";
            return false;
        }

        if (pedido.Lineas.Any(l => l.CantidadPedida <= 0))
        {
            error = "La cantidad de cada línea debe ser mayor que cero.";
            return false;
        }

        if (pedido.Lineas.Any(l => l.PrecioUnitario <= 0))
        {
            error = "Indique el precio de cada producto pedido.";
            return false;
        }

        if (pedido.ClienteId == 0)
        {
            error = "Seleccione el cliente que hace el pedido.";
            return false;
        }

        // Hace falta un producto por línea como máximo: es la clave con la que se suma cuánto
        // lleva despachado cada línea (ProductoId, no un Id de línea que un despacho no conoce).
        var repetido = pedido.Lineas.GroupBy(l => l.ProductoId).FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
        {
            error = $"{repetido.First().ProductoNombre} está en más de una línea; júntelas en una sola.";
            return false;
        }

        error = null;
        return true;
    }

    // --- Transiciones ---

    public Pedido Registrar(Pedido pedido, int usuarioId)
    {
        if (pedido.Id != 0)
            throw new InvalidOperationException("Este pedido ya está registrado.");

        if (!_sesion.Puede(Permisos.Pedidos.Crear))
            throw new InvalidOperationException("No tienes permiso para registrar pedidos.");

        if (!Validar(pedido, out var error))
            throw new InvalidOperationException(error);

        pedido.Numero = SiguienteNumero();
        pedido.Estado = EstadoPedido.Registrado;
        pedido.CreadoPorId = usuarioId;
        pedido.CreadoPorNombre = _sesion.UsuarioActual?.NombreCompleto ?? string.Empty;
        pedido.FechaCreacion = DateTime.Now;
        pedido.Total = pedido.Lineas.Sum(l => l.Subtotal);

        return _pedidos.Add(pedido);
    }

    /// <summary>
    /// Anula el pedido: deja de admitir despachos desde él. NO revierte los despachos que ya haya
    /// generado —mismo criterio que <see cref="ProcesosProduccionService.Anular"/> con las salidas
    /// de un proceso—; anular uno ya Completado es válido, solo cierra el expediente.
    /// </summary>
    public Pedido Anular(Pedido pedido, string motivo)
    {
        if (!PuedeAnular(pedido))
            throw new InvalidOperationException("Solo se puede anular un pedido registrado.");

        if (!_sesion.Puede(Permisos.Pedidos.Anular))
            throw new InvalidOperationException("No tienes permiso para anular pedidos.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Indique el motivo de la anulación.");

        var copia = pedido.Clonar();
        copia.Estado = EstadoPedido.Anulado;
        copia.MotivoAnulacion = motivo.Trim();
        copia.FechaAnulacion = DateTime.Now;
        _pedidos.Update(copia);
        return copia;
    }

    // --- Cuánto se despachó ---

    /// <summary>
    /// Rellena <see cref="PedidoLinea.Despachado"/> sobre los pedidos que ya están en pantalla, con
    /// una sola pasada por la tabla de despachos —igual criterio que
    /// <see cref="ProductosService.RellenarExistencias"/>.
    /// </summary>
    public void RellenarDespachado(IEnumerable<Pedido> pedidos)
    {
        var lista = pedidos.ToList();
        var ids = lista.Select(p => p.Id).ToHashSet();
        var saldos = new Dictionary<(int PedidoId, int ProductoId), decimal>();

        foreach (var despacho in _despachos.GetAll().Where(d => d.CuentaEnExistencia))
            foreach (var linea in despacho.Lineas)
                if (linea.PedidoId is { } pedidoId && ids.Contains(pedidoId))
                {
                    var clave = (pedidoId, linea.ProductoId);
                    saldos[clave] = saldos.GetValueOrDefault(clave) + linea.Cantidad;
                }

        foreach (var pedido in lista)
            foreach (var linea in pedido.Lineas)
                linea.Despachado = saldos.GetValueOrDefault((pedido.Id, linea.ProductoId));
    }

    /// <summary>Pedidos Registrados que todavía no se completaron. Alimenta el dashboard e Inicio.</summary>
    public int PedidosPendientes()
    {
        var pedidos = _pedidos.GetAll().Where(p => p.Estado == EstadoPedido.Registrado).ToList();
        RellenarDespachado(pedidos);
        return pedidos.Count(p => p.EstadoEntregaTexto != "Completado");
    }

    private string SiguienteNumero()
    {
        var ultimo = _pedidos.GetAll()
            .Select(p => p.Numero)
            .Where(n => n.StartsWith(Prefijo, StringComparison.Ordinal))
            .Select(n => int.TryParse(n[Prefijo.Length..], out var valor) ? valor : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{Prefijo}{ultimo + 1:D6}";
    }

    // --- Resúmenes para el panel del módulo ---

    public IReadOnlyList<Pedido> DelMes()
    {
        var desde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        return [.. _pedidos.GetAll()
            .Where(p => p.Estado != EstadoPedido.Anulado && p.Fecha.Date >= desde)];
    }
}
