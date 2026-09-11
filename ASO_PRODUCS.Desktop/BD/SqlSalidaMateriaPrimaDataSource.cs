using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>La salida se guarda entera con sus líneas.</summary>
public class SqlSalidaMateriaPrimaDataSource
    : SqlAgregadoDataSource<SalidaMateriaPrima, int>, ISalidaMateriaPrimaDataSource
{
    protected override IQueryable<SalidaMateriaPrima> Incluir(IQueryable<SalidaMateriaPrima> consulta)
        => consulta.Include(s => s.Lineas);

    /// <summary>Lo más reciente arriba: el historial se lee de vuelta desde hoy.</summary>
    protected override IQueryable<SalidaMateriaPrima> Ordenar(IQueryable<SalidaMateriaPrima> consulta)
        => consulta.OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id);

    protected override Expression<Func<SalidaMateriaPrima, bool>> PorId(int id) => s => s.Id == id;

    protected override IEnumerable<object> HijosDe(SalidaMateriaPrima raiz) => raiz.Lineas;

    protected override void CopiarHijos(SalidaMateriaPrima destino, SalidaMateriaPrima origen)
        => destino.Lineas = origen.Lineas;
}
