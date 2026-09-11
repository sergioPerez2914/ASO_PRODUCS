using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>La factura de cliente se guarda entera con sus líneas.</summary>
public class SqlFacturaClienteDataSource : SqlAgregadoDataSource<FacturaCliente, int>, IFacturaClienteDataSource
{
    protected override IQueryable<FacturaCliente> Incluir(IQueryable<FacturaCliente> consulta)
        => consulta.Include(f => f.Lineas);

    protected override Expression<Func<FacturaCliente, bool>> PorId(int id) => f => f.Id == id;

    protected override IEnumerable<object> HijosDe(FacturaCliente raiz) => raiz.Lineas;

    protected override void CopiarHijos(FacturaCliente destino, FacturaCliente origen)
        => destino.Lineas = origen.Lineas;

    public IEnumerable<FacturaCliente> GetByCliente(int clienteId)
        => Consultar(q => q.Where(f => f.ClienteId == clienteId));
}
