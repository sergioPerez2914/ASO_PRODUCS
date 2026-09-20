namespace ASO_PRODUCS.Desktop.Models;

/// <summary>
/// Artículo del almacén: tapas, etiquetas, detergente, cajas, insumos de la línea.
/// Dato maestro de la organización.
///
/// Es uno de los dos <see cref="IMaterialMaestro"/> del proyecto —el otro es
/// <see cref="TipoMateriaPrima"/>— y los dos se dan de alta con el MISMO formulario.
///
/// No guarda su existencia. La existencia se DERIVA del kardex (lo que entró menos lo que
/// salió, sin contar documentos anulados) porque un número editable a mano se desincroniza del
/// historial y no deja rastro de por qué cambió. Ver <see cref="Existencia"/>.
/// </summary>
public class Articulo : IEntidad<int>, IDeOrganizacion, IMaterialMaestro
{
    /// <summary>Organizacion duenna de la fila; lo estampa AsoProductoresDbContext.SaveChanges.</summary>
    public int OrganizacionId { get; set; }

    public int Id { get; set; }

    /// <summary>
    /// Código con el que la planta identifica el artículo. Único dentro de la organización. Si
    /// quien lo da de alta no escribe uno, <c>InventarioService.GenerarCodigo</c> le pone uno
    /// aleatorio y legible.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Unidad en que se cuenta el artículo (kg, L, Caja…). Texto libre con una lista sugerida
    /// (<see cref="Configuration.UnidadesSugeridas"/>), igual que
    /// <see cref="TipoMateriaPrima.UnidadMedida"/> y <see cref="Producto.UnidadMedida"/>.
    ///
    /// Antes era un enum cerrado heredado de ASO_RTR (Pieza, Caja, Paleta, Kilogramo, Litro,
    /// Metro, Rollo, Saco, Par): paletas, rollos y pares no son vocabulario de esta planta, y era
    /// el único de los tres catálogos que no usaba texto.
    /// </summary>
    public string UnidadMedida { get; set; } = string.Empty;

    /// <summary>Existencia por debajo de la cual el artículo se marca en el almacén. Cero
    /// significa "no vigilar".</summary>
    public decimal Minimo { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>
    /// NO se persiste (va con <c>Ignore</c> en el DbContext): depende de dos tablas enteras y el
    /// modelo no tiene acceso a la base. La rellena <c>InventarioService.RellenarExistencias</c>
    /// antes de mostrar la lista, igual que <see cref="CuentaBancaria.SaldoActual"/>.
    /// </summary>
    public decimal Existencia { get; set; }

    /// <summary>
    /// Precio promedio ponderado de las compras de este artículo. Tampoco se persiste, y por el
    /// mismo motivo que <see cref="Existencia"/>: sale del historial de entradas. La rellena
    /// <c>CostosMaterialesService.RellenarPrecios</c> antes de mostrar la lista.
    ///
    /// Cero significa "no hay ninguna compra con precio" (un artículo cargado solo con entradas de
    /// Ajuste), no "sale gratis": por eso <see cref="PrecioPromedioTexto"/> lo pinta como "—".
    /// </summary>
    public decimal PrecioPromedio { get; set; }

    public string PrecioPromedioTexto => PrecioPromedio > 0 ? PrecioPromedio.ToString("N2") : "—";

    public bool BajoMinimo => Activo && Minimo > 0 && Existencia < Minimo;

    public bool SinExistencia => Activo && Existencia <= 0;

    public string EstadoTexto => !Activo
        ? "Inactivo"
        : SinExistencia
            ? "Sin existencia"
            : BajoMinimo
                ? "Bajo mínimo"
                : "Disponible";

    public string ExistenciaTexto => Existencia.ToString("N2");

    public string MinimoTexto => Minimo > 0 ? Minimo.ToString("N2") : "—";

    public string Etiqueta => $"{Codigo} · {Nombre}";

    /// <summary>Copia superficial (solo hay tipos de valor y cadenas) para no mutar el original
    /// en la lista.</summary>
    public Articulo Clonar() => (Articulo)MemberwiseClone();
}
