using System.Collections.Generic;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Reglas del catálogo de productos terminados y de su existencia.
///
/// Mismo criterio que <see cref="MateriaPrimaService"/>/<see cref="InventarioService"/>: la
/// existencia no se guarda, se calcula. Lo que produjeron los procesos Terminados menos lo que
/// salió en un despacho, sin contar documentos anulados.
/// </summary>
public sealed class ProductosService
{
    private readonly IProductoDataSource _productos;
    private readonly IProcesoProduccionDataSource _procesos;
    private readonly IDespachoDataSource _despachos;

    public ProductosService(IProductoDataSource productos,
                            IProcesoProduccionDataSource procesos,
                            IDespachoDataSource despachos)
    {
        _productos = productos;
        _procesos = procesos;
        _despachos = despachos;
    }

    // --- Validación del maestro ---

    public bool Validar(Producto producto, out string? error)
    {
        if (string.IsNullOrWhiteSpace(producto.Nombre))
        {
            error = "Indique el nombre del producto.";
            return false;
        }

        var repetido = _productos.GetAll()
            .Where(p => p.Id != producto.Id)
            .Any(p => string.Equals(p.Nombre.Trim(), producto.Nombre.Trim(), System.StringComparison.OrdinalIgnoreCase));

        if (repetido)
        {
            error = $"Ya hay un producto llamado {producto.Nombre.Trim()}.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Un producto que ya se produjo o se despachó no se borra: se desactiva. Borrarlo dejaría
    /// esos documentos apuntando a un producto que no existe y la existencia sin poder cuadrarse.
    /// </summary>
    public bool PuedeEliminar(Producto producto)
        => !_procesos.GetAll().Any(p => p.ProductoId == producto.Id)
           && !_despachos.GetAll().Any(d => d.Lineas.Any(l => l.ProductoId == producto.Id));

    /// <summary>
    /// Da de alta, de una sola vez, los productos sugeridos que todavía no existan por nombre
    /// (reutiliza <see cref="Validar"/>, que ya rechaza los repetidos). Pensado para precargar el
    /// catálogo de una planta nueva; correrlo más de una vez no duplica nada. Devuelve cuántos se
    /// crearon.
    /// </summary>
    public int CargarSugeridos(IEnumerable<(string Nombre, string UnidadMedida)> sugeridos)
    {
        var creados = 0;

        foreach (var (nombre, unidadMedida) in sugeridos)
        {
            var candidato = new Producto { Nombre = nombre, UnidadMedida = unidadMedida, Activo = true };

            if (!Validar(candidato, out _))
                continue;

            _productos.Add(candidato);
            creados++;
        }

        return creados;
    }

    // --- Existencia ---

    /// <summary>
    /// Existencia de cada producto que se haya movido alguna vez: lo producido por los procesos
    /// Terminados menos lo despachado, sin contar documentos anulados.
    ///
    /// Recorre las dos tablas UNA vez cada una y suma en memoria sobre un diccionario, igual que
    /// <see cref="MateriaPrimaService.ExistenciasPorTipo"/>.
    /// </summary>
    public IReadOnlyDictionary<int, decimal> ExistenciasPorProducto()
    {
        var saldos = new Dictionary<int, decimal>();

        foreach (var proceso in _procesos.GetAll().Where(p => p.CuentaEnExistencia))
            saldos[proceso.ProductoId] = saldos.GetValueOrDefault(proceso.ProductoId) + (proceso.CantidadProducida ?? 0);

        foreach (var despacho in _despachos.GetAll().Where(d => d.CuentaEnExistencia))
            foreach (var linea in despacho.Lineas)
                saldos[linea.ProductoId] = saldos.GetValueOrDefault(linea.ProductoId) - linea.Cantidad;

        return saldos;
    }

    public decimal Existencia(int productoId) => ExistenciasPorProducto().GetValueOrDefault(productoId);

    /// <summary>
    /// Rellena <see cref="Producto.Existencia"/> sobre los productos que ya están en pantalla.
    ///
    /// Los modelos no avisan de sus cambios, así que quien llame a esto tiene que refrescar la
    /// vista después (<c>ItemsView.Refresh()</c>) o la grilla seguirá mostrando lo anterior.
    /// </summary>
    public void RellenarExistencias(IEnumerable<Producto> productos)
    {
        var saldos = ExistenciasPorProducto();

        foreach (var producto in productos)
            producto.Existencia = saldos.GetValueOrDefault(producto.Id);
    }

    /// <summary>Productos activos con existencia, para elegir al iniciar un proceso o registrar
    /// un despacho.</summary>
    public IReadOnlyList<Producto> ActivosConExistencia()
    {
        var productos = _productos.GetActivos().ToList();
        RellenarExistencias(productos);
        return productos;
    }

    // --- Resúmenes para el panel del módulo ---

    public int TotalProductosActivos() => _productos.GetAll().Count(p => p.Activo);

    public int ProductosSinExistencia()
    {
        var productos = _productos.GetAll().ToList();
        RellenarExistencias(productos);
        return productos.Count(p => p.SinExistencia);
    }
}
