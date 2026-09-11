using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.BD;

/// <summary>La recepción se guarda entera con sus líneas.</summary>
public class SqlRecepcionMateriaPrimaDataSource
    : SqlAgregadoDataSource<RecepcionMateriaPrima, int>, IRecepcionMateriaPrimaDataSource
{
    protected override IQueryable<RecepcionMateriaPrima> Incluir(IQueryable<RecepcionMateriaPrima> consulta)
        => consulta.Include(r => r.Lineas);

    /// <summary>Lo más reciente arriba: el historial se lee de vuelta desde hoy.</summary>
    protected override IQueryable<RecepcionMateriaPrima> Ordenar(IQueryable<RecepcionMateriaPrima> consulta)
        => consulta.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id);

    protected override Expression<Func<RecepcionMateriaPrima, bool>> PorId(int id) => r => r.Id == id;

    protected override IEnumerable<object> HijosDe(RecepcionMateriaPrima raiz) => raiz.Lineas;

    protected override void CopiarHijos(RecepcionMateriaPrima destino, RecepcionMateriaPrima origen)
        => destino.Lineas = origen.Lineas;
}
