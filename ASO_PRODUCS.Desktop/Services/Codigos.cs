using System;
using System.Collections.Generic;
using System.Linq;

namespace ASO_PRODUCS.Desktop.Services;

/// <summary>
/// El código legible de un material maestro: "ART-K7P2Q" en el almacén, "MAT-K7P2Q" en materia
/// prima.
///
/// Vivía dentro de <see cref="InventarioService"/>, que era el único catálogo con código. Desde
/// que los dos formularios son el mismo, los dos lo necesitan y el generador es uno solo — si se
/// duplicara, el alfabeto y la longitud podrían separarse sin que nadie lo note.
/// </summary>
public static class Codigos
{
    /// <summary>
    /// Alfabeto del código generado, sin los caracteres que se confunden al dictar o al leer un
    /// papel: no lleva 0/O ni 1/I/L.
    /// </summary>
    public const string Alfabeto = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public const int Largo = 5;

    /// <summary>
    /// Código aleatorio y legible con el prefijo que se le pase.
    ///
    /// Comprueba contra los que ya existen, pero la garantía de verdad es el índice único
    /// <c>(OrganizacionId, Codigo)</c> de cada tabla: si dos puestos generasen el mismo candidato
    /// a la vez, el segundo choca contra la base en vez de duplicar la fila.
    /// </summary>
    public static string Generar(string prefijo, IEnumerable<string> usados, string queEs)
    {
        var tomados = usados.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var intento = 0; intento < 20; intento++)
        {
            var candidato = prefijo + new string(Enumerable.Range(0, Largo)
                .Select(_ => Alfabeto[Random.Shared.Next(Alfabeto.Length)])
                .ToArray());

            if (!tomados.Contains(candidato))
                return candidato;
        }

        throw new InvalidOperationException(
            $"No se pudo generar un código libre para {queEs}; escriba uno a mano.");
    }
}
