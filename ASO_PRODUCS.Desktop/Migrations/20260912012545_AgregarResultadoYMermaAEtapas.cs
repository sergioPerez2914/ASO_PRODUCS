using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarResultadoYMermaAEtapas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Motivo",
                table: "SalidasMateriaPrima",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Motivo",
                table: "EtapaProcesoProduccionLinea",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Resultado",
                table: "EtapaProcesoProduccion",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "SalidasMateriaPrima");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "EtapaProcesoProduccionLinea");

            migrationBuilder.DropColumn(
                name: "Resultado",
                table: "EtapaProcesoProduccion");
        }
    }
}
