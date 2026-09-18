using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// Un documento listo para el papel: cabecera con los datos que lo identifican, una tabla de
/// líneas y los totales al pie.
///
/// Los valores llegan YA COMO TEXTO, igual que en <see cref="HojaExcel"/> y por el mismo motivo:
/// las propiedades "…Texto" de los modelos son las que ya deciden cómo se escribe un monto o una
/// fecha en pantalla, y el papel no puede decir otra cosa que la pantalla.
/// </summary>
/// <param name="Titulo">Qué documento es: "Boleto de salida", "Despacho".</param>
/// <param name="Numero">El correlativo, que es lo que se cita por teléfono: "SAL-000123".</param>
/// <param name="Encabezado">Los datos de la cabecera, en pares etiqueta/valor.</param>
/// <param name="Columnas">Encabezados de la tabla de líneas.</param>
/// <param name="Lineas">Una lista por fila, con tantos textos como columnas.</param>
/// <param name="Totales">Los pares del pie: unidades, monto. Puede ir vacío.</param>
/// <param name="Firma">Rótulo de la línea de firma, o null si el documento no se firma.</param>
public sealed record DocumentoImprimible(
    string Titulo,
    string Numero,
    IReadOnlyList<(string Etiqueta, string Valor)> Encabezado,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<string>> Lineas,
    IReadOnlyList<(string Etiqueta, string Valor)> Totales,
    string? Firma = null);

/// <summary>
/// Imprime documentos con el cuadro de impresión de Windows.
///
/// <para><b>Por qué hacía falta.</b> No había impresión en ninguna parte de la aplicación (cero
/// <c>PrintDialog</c>), y sin embargo el negocio entrega papel: un boleto de salida viaja con la
/// mercancía, un despacho lo firma quien recibe, una recepción respalda lo que entró. El sistema
/// ya se ocupa de que el papel no mienta —el autorizador lo estampa el servicio con el usuario de
/// la sesión, no se teclea— pero luego no había forma de sacarlo.</para>
///
/// <para>Se construye un <see cref="FlowDocument"/> en código y no una plantilla XAML: el
/// contenido es tabular y variable (cada documento trae sus columnas), y una plantilla tendría
/// que declarar todas las formas posibles. Sin paquete nuevo — WPF ya trae la impresión, igual
/// que ClosedXML se trajo solo porque Excel sí lo necesitaba.</para>
///
/// <para><b>Deliberadamente en blanco y negro y sin logo.</b> Lo que sale por una impresora de
/// oficina es texto: los colores del tema no sobreviven al papel (el bronce de marca sobre blanco
/// queda en un gris ilegible) y el logo a 100 ppp se ensucia. Lo que identifica el papel es el
/// nombre de la organización y el número del documento, que es lo que se cita cuando alguien
/// llama preguntando por él.</para>
/// </summary>
public static class ImpresionDocumento
{
    private const double MargenPagina = 48;      // ~1,7 cm
    private const double CuerpoPt = 11;
    private const double TituloPt = 18;

    /// <summary>
    /// Abre el cuadro de impresión y, si se acepta, manda el documento.
    /// </summary>
    /// <returns><c>true</c> si se envió a imprimir; <c>false</c> si se canceló.</returns>
    public static bool Imprimir(DocumentoImprimible documento)
    {
        var dialogo = new PrintDialog();

        if (dialogo.ShowDialog() != true)
            return false;

        var flujo = Construir(documento, dialogo.PrintableAreaWidth, dialogo.PrintableAreaHeight);

        dialogo.PrintDocument(((IDocumentPaginatorSource)flujo).DocumentPaginator,
                              $"{documento.Titulo} {documento.Numero}".Trim());

        return true;
    }

    private static FlowDocument Construir(DocumentoImprimible documento, double ancho, double alto)
    {
        var flujo = new FlowDocument
        {
            // Una serifa para el papel: es lo que se lee en un documento impreso, y además no
            // depende de que Satoshi —que viaja empotrada en el .exe— exista en el controlador
            // de la impresora.
            FontFamily = new FontFamily("Georgia, Times New Roman, serif"),
            FontSize = CuerpoPt,
            PagePadding = new Thickness(MargenPagina),
            ColumnWidth = double.PositiveInfinity,   // una sola columna, no periódico
            PageWidth = ancho,
            PageHeight = alto,
            Foreground = Brushes.Black,
            Background = Brushes.White
        };

        flujo.Blocks.Add(Cabecera(documento));

        if (documento.Encabezado.Count > 0)
            flujo.Blocks.Add(Pares(documento.Encabezado));

        if (documento.Lineas.Count > 0)
            flujo.Blocks.Add(TablaDeLineas(documento));

        if (documento.Totales.Count > 0)
            flujo.Blocks.Add(Pares(documento.Totales, negrita: true));

        if (documento.Firma is { Length: > 0 } firma)
            flujo.Blocks.Add(LineaDeFirma(firma));

        flujo.Blocks.Add(PieDeImpresion());

        return flujo;
    }

    private static Block Cabecera(DocumentoImprimible documento)
    {
        var bloque = new Paragraph { Margin = new Thickness(0, 0, 0, 16) };

        if (Ambito.Actual is { } organizacion)
        {
            bloque.Inlines.Add(new Run(organizacion.Nombre) { FontSize = CuerpoPt + 1 });
            bloque.Inlines.Add(new LineBreak());
        }

        bloque.Inlines.Add(new Run(documento.Titulo)
        {
            FontSize = TituloPt,
            FontWeight = FontWeights.Bold
        });

        if (documento.Numero is { Length: > 0 })
        {
            bloque.Inlines.Add(new Run("  " + documento.Numero)
            {
                FontSize = TituloPt,
                FontWeight = FontWeights.Bold
            });
        }

        return bloque;
    }

    /// <summary>
    /// Pares etiqueta/valor en dos columnas. Tabla y no tabulaciones: con textos largos —el
    /// nombre de un cliente, un motivo de anulación— las tabulaciones se descuadran y el valor
    /// se sale del renglón.
    /// </summary>
    private static Block Pares(IReadOnlyList<(string Etiqueta, string Valor)> pares, bool negrita = false)
    {
        var tabla = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 16) };
        tabla.Columns.Add(new TableColumn { Width = new GridLength(150, GridUnitType.Pixel) });
        tabla.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

        var grupo = new TableRowGroup();

        foreach (var (etiqueta, valor) in pares)
        {
            var fila = new TableRow();
            fila.Cells.Add(Celda(etiqueta, negrita: false, gris: true));
            fila.Cells.Add(Celda(valor, negrita));
            grupo.Rows.Add(fila);
        }

        tabla.RowGroups.Add(grupo);
        return tabla;
    }

    private static Block TablaDeLineas(DocumentoImprimible documento)
    {
        var tabla = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 16) };

        // La primera columna es el concepto y se lleva el espacio sobrante; las demás son datos
        // cortos (cantidad, precio) y se reparten por igual.
        for (var i = 0; i < documento.Columnas.Count; i++)
        {
            tabla.Columns.Add(new TableColumn
            {
                Width = new GridLength(i == 0 ? 3 : 1, GridUnitType.Star)
            });
        }

        var encabezado = new TableRowGroup();
        var filaEncabezado = new TableRow();

        foreach (var columna in documento.Columnas)
            filaEncabezado.Cells.Add(Celda(columna, negrita: true, borde: true));

        encabezado.Rows.Add(filaEncabezado);
        tabla.RowGroups.Add(encabezado);

        var cuerpo = new TableRowGroup();

        foreach (var linea in documento.Lineas)
        {
            var fila = new TableRow();

            foreach (var valor in linea)
                fila.Cells.Add(Celda(valor, negrita: false, borde: true));

            cuerpo.Rows.Add(fila);
        }

        tabla.RowGroups.Add(cuerpo);
        return tabla;
    }

    private static TableCell Celda(string texto, bool negrita, bool gris = false, bool borde = false)
        => new(new Paragraph(new Run(texto ?? string.Empty))
        {
            Margin = new Thickness(0, 3, 8, 3),
            FontWeight = negrita ? FontWeights.Bold : FontWeights.Normal,
            Foreground = gris ? Brushes.DimGray : Brushes.Black
        })
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = borde ? new Thickness(0, 0, 0, 0.5) : default
        };

    private static Block LineaDeFirma(string rotulo)
    {
        var bloque = new Paragraph { Margin = new Thickness(0, 40, 0, 0) };
        bloque.Inlines.Add(new Run(new string('_', 40)));
        bloque.Inlines.Add(new LineBreak());
        bloque.Inlines.Add(new Run(rotulo) { Foreground = Brushes.DimGray });
        return bloque;
    }

    /// <summary>Cuándo se imprimió. Sin esto, dos copias de distintas fechas no se distinguen.</summary>
    private static Block PieDeImpresion()
        => new Paragraph(new Run($"Impreso el {DateTime.Now:dd/MM/yyyy HH:mm}"))
        {
            Margin = new Thickness(0, 24, 0, 0),
            FontSize = CuerpoPt - 2,
            Foreground = Brushes.DimGray
        };
}
