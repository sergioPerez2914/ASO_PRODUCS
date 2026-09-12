using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProveedorARecepcionesMateriaPrima : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecioUnitario",
                table: "RecepcionMateriaPrimaLinea",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "RecepcionMateriaPrimaLinea",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "FacturaProveedorId",
                table: "RecepcionesMateriaPrima",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacturaProveedorNumero",
                table: "RecepcionesMateriaPrima",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVencimiento",
                table: "RecepcionesMateriaPrima",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDocumento",
                table: "RecepcionesMateriaPrima",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProveedorId",
                table: "RecepcionesMateriaPrima",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProveedorNombre",
                table: "RecepcionesMateriaPrima",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecibidoPor",
                table: "RecepcionesMateriaPrima",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "RecepcionesMateriaPrima",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "RecepcionesMateriaPrima",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Las recepciones que ya existían se registraron antes de que este módulo distinguiera
            // el origen: ninguna generó cuenta por pagar, así que el default de la columna (0 =
            // CompraProveedor) las etiquetaría mal. Se corrigen a OtroOrigen (2), que es lo que de
            // verdad representaron.
            migrationBuilder.Sql("UPDATE RecepcionesMateriaPrima SET Tipo = 2");

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesMateriaPrima_FacturaProveedorId",
                table: "RecepcionesMateriaPrima",
                column: "FacturaProveedorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecepcionesMateriaPrima_FacturaProveedorId",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "PrecioUnitario",
                table: "RecepcionMateriaPrimaLinea");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "RecepcionMateriaPrimaLinea");

            migrationBuilder.DropColumn(
                name: "FacturaProveedorId",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "FacturaProveedorNumero",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "FechaVencimiento",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "NumeroDocumento",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "ProveedorId",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "ProveedorNombre",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "RecibidoPor",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "RecepcionesMateriaPrima");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "RecepcionesMateriaPrima");
        }
    }
}
