using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCierreAEtapas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsCierre",
                table: "EtapaProcesoProduccion",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsCierre",
                table: "EtapaProcesoProduccion");
        }
    }
}
