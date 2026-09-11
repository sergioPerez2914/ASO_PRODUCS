using System.Collections.Generic;
using ClosedXML.Excel;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>Una hoja del libro a exportar: su nombre, los encabezados de columna y las filas, ya
/// como texto formateado (las mismas propiedades "...Texto" que ya usan los modelos para
/// pintarse en pantalla) — exportar no repite ninguna lógica de formato de números o fechas.</summary>
public sealed record HojaExcel(string Nombre, IReadOnlyList<string> Encabezados, IReadOnlyList<IReadOnlyList<string>> Filas);

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

            for (var columna = 0; columna < hoja.Encabezados.Count; columna++)
                pestana.Cell(1, columna + 1).Value = hoja.Encabezados[columna];

            pestana.Row(1).Style.Font.Bold = true;

            for (var fila = 0; fila < hoja.Filas.Count; fila++)
            {
                var valores = hoja.Filas[fila];

                for (var columna = 0; columna < valores.Count; columna++)
                    pestana.Cell(fila + 2, columna + 1).Value = valores[columna];
            }

            pestana.Columns().AdjustToContents();
        }

        libro.SaveAs(rutaArchivo);
    }
}
