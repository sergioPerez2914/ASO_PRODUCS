using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Ficha de un material del almacén o de materia prima: su precio promedio, las compras de las que
/// sale ese promedio, lo que debería quedar de cada una, y sus salidas.
///
/// NO se persiste: la arma <c>CostosMaterialesService.Ficha</c> cada vez que se abre, igual que la
/// existencia (<see cref="Articulo.Existencia"/>) y que el costo de un proceso
/// (<see cref="CostoProceso"/>).
///
/// El promedio se calcula con la MISMA regla que usa el costo de producción: solo las compras con
/// precio > 0 (un Ajuste de Inventario o una recepción OtroOrigen entran a cero y arrastrarían el
/// promedio hacia abajo sin ser un costo real). Esas entradas sin precio SÍ están en
/// <see cref="Compras"/>, porque sí suman a la existencia: si no estuvieran, la suma de los
/// <see cref="CompraMaterial.Restante"/> no cuadraría con <see cref="Existencia"/>.
/// </summary>
public sealed class FichaMaterial
{
    public OrigenMaterial Origen { get; init; }
    public int MaterialId { get; init; }

    public string Nombre { get; init; } = string.Empty;

    public string Codigo { get; init; } = string.Empty;

    public string Unidad { get; init; } = string.Empty;

    public decimal Existencia { get; init; }

    public decimal Minimo { get; init; }

    /// <summary>El mismo texto que pinta el chip de la grilla ("Disponible", "Bajo mínimo"…), para
    /// que la ficha reutilice <c>ChipExistenciaStyle</c> sin recalcular nada.</summary>
    public string EstadoTexto { get; init; } = string.Empty;

    /// <summary>Todas las entradas del material, de la más vieja a la más nueva, con el reparto
    /// FIFO ya hecho.</summary>
    public IReadOnlyList<CompraMaterial> Compras { get; init; } = [];

    /// <summary>Todas las salidas del material, de la más nueva a la más vieja.</summary>
    public IReadOnlyList<ConsumoMaterial> Salidas { get; init; } = [];

    public IEnumerable<CompraMaterial> ConPrecio => Compras.Where(c => c.CuentaParaPromedio);

    /// <summary>Nulo si no hay ninguna compra con precio: no es que valga cero, es que no se
    /// sabe.</summary>
    public decimal? PrecioPromedio
    {
        get
        {
            var cantidad = ConPrecio.Sum(c => c.Cantidad);
            return cantidad > 0 ? ConPrecio.Sum(c => c.Subtotal) / cantidad : null;
        }
    }

    /// <summary>La compra con precio más reciente: <see cref="Compras"/> va de la más vieja a la
    /// más nueva, así que es la última de la lista.</summary>
    public CompraMaterial? UltimaCompraConPrecio => ConPrecio.LastOrDefault();

    public decimal? UltimoPrecio => UltimaCompraConPrecio?.PrecioUnitario;

    public decimal? ValorInventario => PrecioPromedio is { } promedio ? Existencia * promedio : null;

    public decimal TotalComprado => Compras.Sum(c => c.Cantidad);

    public decimal TotalConsumido => Salidas.Sum(s => s.Cantidad);

    public int ComprasConPrecio => ConPrecio.Count();

    /// <summary>No hay de dónde sacar un precio: el promedio sale "—" y la ficha lo avisa.</summary>
    public bool SinPrecio => PrecioPromedio is null;

    /// <summary>Hay entradas a precio cero en la lista: la ficha explica por qué no cuentan.</summary>
    public bool HayEntradasSinPrecio => Compras.Any(c => !c.CuentaParaPromedio);

    public bool BajoMinimo => Minimo > 0 && Existencia < Minimo;

    public string Subtitulo => Codigo;

    public string ExistenciaTexto => $"{Existencia:N2} {Unidad}".Trim();
    public string MinimoTexto => Minimo > 0 ? $"{Minimo:N2} {Unidad}".Trim() : "—";
    public string TotalCompradoTexto => $"{TotalComprado:N2} {Unidad}".Trim();
    public string TotalConsumidoTexto => $"{TotalConsumido:N2} {Unidad}".Trim();
    public string PrecioPromedioTexto => PrecioPromedio is { } p ? p.ToString("N2") : "—";
    public string UltimoPrecioTexto => UltimoPrecio is { } p ? p.ToString("N2") : "—";
    public string ValorInventarioTexto => ValorInventario is { } v ? v.ToString("N2") : "—";

    public string UltimoPrecioNota => UltimaCompraConPrecio is { } compra
        ? $"{compra.FechaTexto} · {compra.Numero}"
        : "Ninguna compra con precio";

    public string PromedioNota => SinPrecio
        ? "Sin compras con precio"
        : ComprasConPrecio == 1
            ? "De 1 compra con precio"
            : $"De {ComprasConPrecio} compras con precio";
}

/// <summary>
/// Una entrada del material (una línea de entrada de almacén o de recepción de materia prima), con
/// lo que ya se consumió de ella y lo que debería quedar.
/// </summary>
public sealed class CompraMaterial
{
    public DateTime Fecha { get; init; }
    public string Numero { get; init; } = string.Empty;
    public string OrigenTexto { get; init; } = string.Empty;

    /// <summary>Número de la factura o el recibo del proveedor. Vacío si no hubo papel.</summary>
    public string DocumentoTexto { get; init; } = string.Empty;

    public string Unidad { get; init; } = string.Empty;
    public decimal Cantidad { get; init; }

    /// <summary>Cero en un Ajuste de Inventario o en una recepción OtroOrigen.</summary>
    public decimal PrecioUnitario { get; init; }

    /// <summary>Lo que el reparto FIFO imputó a esta compra. Lo llena
    /// <c>CostosMaterialesService.Ficha</c>, no el constructor.</summary>
    public decimal Consumido { get; set; }

    /// <summary>Se calcula igual que en <c>CostosProduccionService</c> (cantidad × precio) en vez de
    /// leer el <c>Subtotal</c> guardado, para que el promedio de la ficha y el costo de un proceso
    /// no puedan diferir por un redondeo del documento.</summary>
    public decimal Subtotal => Cantidad * PrecioUnitario;

    public bool CuentaParaPromedio => PrecioUnitario > 0 && Cantidad > 0;

    public decimal Restante => Cantidad - Consumido;

    public bool Agotada => Restante <= 0;

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string CantidadTexto => $"{Cantidad:N2} {Unidad}".Trim();
    public string ConsumidoTexto => $"{Consumido:N2} {Unidad}".Trim();
    public string RestanteTexto => $"{Restante:N2} {Unidad}".Trim();
    public string PrecioUnitarioTexto => CuentaParaPromedio ? PrecioUnitario.ToString("N2") : "—";
    public string SubtotalTexto => CuentaParaPromedio ? Subtotal.ToString("N2") : "—";
    public string DetalleTexto => string.IsNullOrWhiteSpace(DocumentoTexto)
        ? OrigenTexto
        : $"{OrigenTexto} · Nº {DocumentoTexto}";
}

/// <summary>Una salida del material (una línea de boleto de salida o de salida de materia prima).</summary>
public sealed class ConsumoMaterial
{
    public DateTime Fecha { get; init; }
    public string Numero { get; init; } = string.Empty;
    public string Unidad { get; init; } = string.Empty;
    public decimal Cantidad { get; init; }

    /// <summary>El <c>MotivoDetalleTexto</c> del documento: dice el motivo y, si la salida vino de
    /// un proceso de producción, cuál.</summary>
    public string MotivoTexto { get; init; } = string.Empty;

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string CantidadTexto => $"{Cantidad:N2} {Unidad}".Trim();
}
