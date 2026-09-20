using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <summary>
    /// Los dos catálogos de materiales (Articulos, TiposMateriaPrima) pasan a tener los mismos
    /// cinco campos, porque los dos se dan de alta con el mismo formulario.
    ///
    /// EF no puede escribir esto solo: el andamiaje generado dejaba el DROP de "Unidad" ANTES del
    /// ADD de "UnidadMedida" (imposible convertir nada) y el índice único de "Codigo" ANTES de
    /// rellenarlo (todas las filas chocarían entre sí con la cadena vacía). El orden de abajo es a
    /// mano y a propósito.
    /// </summary>
    public partial class UnificaCatalogosDeMateriales : Migration
    {
        /// <summary>Alfabeto de <c>Services/Codigos.cs</c>: sin 0/O ni 1/I/L, que se confunden al
        /// dictar o al leer un papel.</summary>
        private const string Alfabeto = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Articulos: el enum de unidades pasa a texto ---

            migrationBuilder.AddColumn<string>(
                name: "UnidadMedida",
                table: "Articulos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // Cada ordinal a su palabra, ANTES de tirar la columna vieja. "Paleta", "Metro",
            // "Rollo" y "Par" sobreviven como texto aunque el desplegable deje de ofrecerlos: eran
            // vocabulario de ASO_RTR, pero si algún artículo los usa no se le pisa el dato.
            migrationBuilder.Sql("""
                UPDATE Articulos
                SET UnidadMedida = CASE Unidad
                    WHEN 0 THEN N'Pieza'
                    WHEN 1 THEN N'Caja'
                    WHEN 2 THEN N'Paleta'
                    WHEN 3 THEN N'kg'
                    WHEN 4 THEN N'L'
                    WHEN 5 THEN N'Metro'
                    WHEN 6 THEN N'Rollo'
                    WHEN 7 THEN N'Saco'
                    WHEN 8 THEN N'Par'
                    ELSE N'Unidad'
                END;
                """);

            migrationBuilder.DropColumn(name: "Unidad", table: "Articulos");

            // --- Articulos: lo que se va porque materia prima no lo tiene ---
            // Ubicacion era de ASO_RTR (pasillo/estante de una embotelladora). Categoria y Notas
            // se van para que los dos formularios queden idénticos. Su contenido se pierde.

            migrationBuilder.DropColumn(name: "Categoria", table: "Articulos");
            migrationBuilder.DropColumn(name: "Notas", table: "Articulos");
            migrationBuilder.DropColumn(name: "Ubicacion", table: "Articulos");

            // --- TiposMateriaPrima: gana código, como el almacén ---

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "TiposMateriaPrima",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Los tipos que ya existían nacen sin código, y el índice único de abajo los
            // rechazaría a todos por la cadena vacía. Se rellenan codificando en base 31 el número
            // de fila sobre el alfabeto legible: determinista, sin colisiones, y con la misma forma
            // que los que genera la aplicación ("MAT-32345").
            migrationBuilder.Sql($"""
                DECLARE @Alfabeto nchar(31) = N'{Alfabeto}';

                WITH Numerados AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) - 1 AS N
                    FROM TiposMateriaPrima
                    WHERE Codigo = N''
                )
                UPDATE t
                SET Codigo = N'MAT-'
                    + SUBSTRING(@Alfabeto, (n.N / 923521) % 31 + 1, 1)
                    + SUBSTRING(@Alfabeto, (n.N / 29791) % 31 + 1, 1)
                    + SUBSTRING(@Alfabeto, (n.N / 961) % 31 + 1, 1)
                    + SUBSTRING(@Alfabeto, (n.N / 31) % 31 + 1, 1)
                    + SUBSTRING(@Alfabeto, n.N % 31 + 1, 1)
                FROM TiposMateriaPrima t
                INNER JOIN Numerados n ON n.Id = t.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TiposMateriaPrima_OrganizacionId_Codigo",
                table: "TiposMateriaPrima",
                columns: new[] { "OrganizacionId", "Codigo" },
                unique: true);
        }

        /// <summary>
        /// Devuelve el esquema, no el dato: <c>Categoria</c>, <c>Notas</c> y <c>Ubicacion</c>
        /// vuelven vacías porque su contenido se borró al subir. La unidad sí se reconstruye para
        /// los textos que este mismo archivo escribió.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TiposMateriaPrima_OrganizacionId_Codigo",
                table: "TiposMateriaPrima");

            migrationBuilder.DropColumn(name: "Codigo", table: "TiposMateriaPrima");

            migrationBuilder.AddColumn<int>(
                name: "Unidad",
                table: "Articulos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE Articulos
                SET Unidad = CASE UnidadMedida
                    WHEN N'Pieza'  THEN 0
                    WHEN N'Caja'   THEN 1
                    WHEN N'Paleta' THEN 2
                    WHEN N'kg'     THEN 3
                    WHEN N'L'      THEN 4
                    WHEN N'Metro'  THEN 5
                    WHEN N'Rollo'  THEN 6
                    WHEN N'Saco'   THEN 7
                    WHEN N'Par'    THEN 8
                    ELSE 0
                END;
                """);

            migrationBuilder.DropColumn(name: "UnidadMedida", table: "Articulos");

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Articulos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notas",
                table: "Articulos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ubicacion",
                table: "Articulos",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");
        }
    }
}
