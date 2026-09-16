using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>El pedido se guarda entero con sus líneas.</summary>
public class SqlPedidoDataSource : SqlAgregadoDataSource<Pedido, int>, IPedidoDataSource
{
    protected override IQueryable<Pedido> Incluir(IQueryable<Pedido> consulta)
        => consulta.Include(p => p.Lineas);

    /// <summary>Lo más reciente arriba: el historial se lee de vuelta desde hoy.</summary>
    protected override IQueryable<Pedido> Ordenar(IQueryable<Pedido> consulta)
        => consulta.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id);

    protected override Expression<Func<Pedido, bool>> PorId(int id) => p => p.Id == id;

    protected override IEnumerable<object> HijosDe(Pedido raiz) => raiz.Lineas;

    protected override void CopiarHijos(Pedido destino, Pedido origen)
        => destino.Lineas = origen.Lineas;
}
