using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class RemplazaCierreConEtapaActual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsCierre",
                table: "EtapaProcesoProduccion");

            migrationBuilder.AddColumn<int>(
                name: "EtapaActualId",
                table: "ProcesosProduccion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EtapaActualNombre",
                table: "ProcesosProduccion",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EtapaActualId",
                table: "ProcesosProduccion");

            migrationBuilder.DropColumn(
                name: "EtapaActualNombre",
                table: "ProcesosProduccion");

            migrationBuilder.AddColumn<bool>(
                name: "EsCierre",
                table: "EtapaProcesoProduccion",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
