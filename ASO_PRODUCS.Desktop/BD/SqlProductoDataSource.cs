using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>Agregado: el producto con los componentes de su receta de presentación.</summary>
public class SqlProductoDataSource : SqlAgregadoDataSource<Producto, int>, IProductoDataSource
{
    protected override IQueryable<Producto> Incluir(IQueryable<Producto> consulta)
        => consulta.Include(p => p.Componentes);

    protected override IQueryable<Producto> Ordenar(IQueryable<Producto> consulta)
        => consulta.OrderBy(p => p.Nombre);

    protected override Expression<Func<Producto, bool>> PorId(int id) => p => p.Id == id;

    protected override IEnumerable<object> HijosDe(Producto raiz) => raiz.Componentes;

    protected override void CopiarHijos(Producto destino, Producto origen)
        => destino.Componentes = origen.Componentes;

    public IEnumerable<Producto> GetActivos()
        => Consultar(q => Incluir(q).Where(p => p.Activo).OrderBy(p => p.Nombre));
}
