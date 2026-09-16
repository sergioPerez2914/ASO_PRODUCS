using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>Catálogo plano, sin hijos: le basta el cuerpo común.</summary>
public class SqlEtapaProduccionDataSource : SqlCrudDataSource<EtapaProduccion, int>, IEtapaProduccionDataSource
{
    protected override IQueryable<EtapaProduccion> Ordenar(IQueryable<EtapaProduccion> consulta)
        => consulta.OrderBy(e => e.Nombre);

    public IEnumerable<EtapaProduccion> GetActivas()
        => Consultar(q => q.Where(e => e.Activo).OrderBy(e => e.Nombre));
}
