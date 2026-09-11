using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>Catálogo plano, sin hijos: le basta el cuerpo común.</summary>
public class SqlTipoMateriaPrimaDataSource : SqlCrudDataSource<TipoMateriaPrima, int>, ITipoMateriaPrimaDataSource
{
    protected override IQueryable<TipoMateriaPrima> Ordenar(IQueryable<TipoMateriaPrima> consulta)
        => consulta.OrderBy(t => t.Nombre);

    public IEnumerable<TipoMateriaPrima> GetActivos()
        => Consultar(q => q.Where(t => t.Activo).OrderBy(t => t.Nombre));
}
