using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>
/// El proceso se guarda entero con sus líneas iniciales y sus etapas.
///
/// Es un agregado de DOS niveles (cada etapa tiene, a su vez, sus propias líneas), pero
/// <see cref="SqlAgregadoDataSource{T, TId}"/> no necesita ningún cambio para soportarlo: al
/// borrar las etapas viejas con <c>context.RemoveRange(HijosDe(existente))</c>, EF Core cascadea
/// el borrado de las líneas owned de cada etapa —un dependiente owned no puede sobrevivir sin su
/// dueño—, así que <see cref="HijosDe"/> solo necesita enumerar los hijos DIRECTOS de la raíz.
/// </summary>
public class SqlProcesoProduccionDataSource
    : SqlAgregadoDataSource<ProcesoProduccion, int>, IProcesoProduccionDataSource
{
    protected override IQueryable<ProcesoProduccion> Incluir(IQueryable<ProcesoProduccion> consulta)
        => consulta.Include(p => p.LineasIniciales)
                   .Include(p => p.Etapas).ThenInclude(e => e.Lineas);

    /// <summary>Lo más reciente arriba: el historial se lee de vuelta desde hoy.</summary>
    protected override IQueryable<ProcesoProduccion> Ordenar(IQueryable<ProcesoProduccion> consulta)
        => consulta.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id);

    protected override Expression<Func<ProcesoProduccion, bool>> PorId(int id) => p => p.Id == id;

    protected override IEnumerable<object> HijosDe(ProcesoProduccion raiz)
        => raiz.Etapas.Cast<object>().Concat(raiz.LineasIniciales);

    protected override void CopiarHijos(ProcesoProduccion destino, ProcesoProduccion origen)
    {
        destino.LineasIniciales = origen.LineasIniciales;
        destino.Etapas = origen.Etapas;
    }
}
