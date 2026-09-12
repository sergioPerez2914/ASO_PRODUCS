using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProcesoOrigenADespacho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcesoProduccionId",
                table: "DespachoLinea",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcesoProduccionNumero",
                table: "DespachoLinea",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcesoProduccionId",
                table: "DespachoLinea");

            migrationBuilder.DropColumn(
                name: "ProcesoProduccionNumero",
                table: "DespachoLinea");
        }
    }
}
