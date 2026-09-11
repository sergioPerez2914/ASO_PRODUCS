using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>Catálogo plano, sin hijos: le basta el cuerpo común.</summary>
public class SqlProductoDataSource : SqlCrudDataSource<Producto, int>, IProductoDataSource
{
    protected override IQueryable<Producto> Ordenar(IQueryable<Producto> consulta)
        => consulta.OrderBy(p => p.Nombre);

    public IEnumerable<Producto> GetActivos()
        => Consultar(q => q.Where(p => p.Activo).OrderBy(p => p.Nombre));
}
