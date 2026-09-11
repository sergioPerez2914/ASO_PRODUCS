using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>El despacho se guarda entero con sus líneas.</summary>
public class SqlDespachoDataSource : SqlAgregadoDataSource<Despacho, int>, IDespachoDataSource
{
    protected override IQueryable<Despacho> Incluir(IQueryable<Despacho> consulta)
        => consulta.Include(d => d.Lineas);

    /// <summary>Lo más reciente arriba: el historial se lee de vuelta desde hoy.</summary>
    protected override IQueryable<Despacho> Ordenar(IQueryable<Despacho> consulta)
        => consulta.OrderByDescending(d => d.Fecha).ThenByDescending(d => d.Id);

    protected override Expression<Func<Despacho, bool>> PorId(int id) => d => d.Id == id;

    protected override IEnumerable<object> HijosDe(Despacho raiz) => raiz.Lineas;

    protected override void CopiarHijos(Despacho destino, Despacho origen)
        => destino.Lineas = origen.Lineas;
}
