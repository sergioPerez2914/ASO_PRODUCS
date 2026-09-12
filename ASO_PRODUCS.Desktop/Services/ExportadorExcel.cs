using System.Collections.Generic;
using ClosedXML.Excel;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>Una hoja del libro a exportar: su nombre, los encabezados de columna y las filas, ya
/// como texto formateado (las mismas propiedades "...Texto" que ya usan los modelos para
/// pintarse en pantalla) — exportar no repite ninguna lógica de formato de números o fechas.
/// <paramref name="Titulo"/> y <paramref name="Periodo"/> son opcionales y van cada uno en su
/// propia línea sobre los encabezados: qué reporte es (típicamente "Reporte de X · Vista") y de
/// qué rango de fechas, para que el archivo diga por sí solo qué es y de cuándo son los datos sin
/// depender de que quien lo reciba se acuerde de preguntarlo.</summary>
public sealed record HojaExcel(string Nombre, IReadOnlyList<string> Encabezados, IReadOnlyList<IReadOnlyList<string>> Filas,
    string? Titulo = null, string? Periodo = null);

/// <summary>
/// Escribe reportes de solo lectura a un archivo .xlsx con ClosedXML. Genérico a propósito: cada
/// pantalla de Reportes arma sus propias <see cref="HojaExcel"/> con lo que ya tiene calculado y
/// solo llama a <see cref="Exportar"/>.
/// </summary>
public static class ExportadorExcel
{
    public static void Exportar(string rutaArchivo, IEnumerable<HojaExcel> hojas)
    {
        using var libro = new XLWorkbook();

        foreach (var hoja in hojas)
        {
            var pestana = libro.Worksheets.Add(hoja.Nombre);

            // Título y período ocupan cada uno su propia fila arriba del encabezado, así que las
            // filas siguientes se corren una posición por cada una que esté presente.
            var filaEncabezado = 1;

            if (hoja.Titulo is { } titulo)
            {
                pestana.Cell(filaEncabezado, 1).Value = titulo;
                pestana.Row(filaEncabezado).Style.Font.Bold = true;
                pestana.Row(filaEncabezado).Style.Font.FontSize = 14;
                filaEncabezado++;
            }

            if (hoja.Periodo is { } periodo)
            {
                pestana.Cell(filaEncabezado, 1).Value = periodo;
                pestana.Row(filaEncabezado).Style.Font.Italic = true;
                filaEncabezado++;
            }

            for (var columna = 0; columna < hoja.Encabezados.Count; columna++)
                pestana.Cell(filaEncabezado, columna + 1).Value = hoja.Encabezados[columna];

            pestana.Row(filaEncabezado).Style.Font.Bold = true;

            for (var fila = 0; fila < hoja.Filas.Count; fila++)
            {
                var valores = hoja.Filas[fila];

                for (var columna = 0; columna < valores.Count; columna++)
                    pestana.Cell(fila + filaEncabezado + 1, columna + 1).Value = valores[columna];
            }

            pestana.Columns().AdjustToContents();
        }

        libro.SaveAs(rutaArchivo);
    }
}
