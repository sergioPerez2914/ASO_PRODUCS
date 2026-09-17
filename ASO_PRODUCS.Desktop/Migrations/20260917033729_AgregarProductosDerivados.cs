using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProductosDerivados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CantidadBasePorUnidad",
                table: "Productos",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductoBaseId",
                table: "Productos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductoBaseNombre",
                table: "Productos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LoteNumero",
                table: "ProcesoProduccionLineaInicial",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoteProcesoId",
                table: "ProcesoProduccionLineaInicial",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoteNumero",
                table: "EtapaProcesoProduccionLinea",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoteProcesoId",
                table: "EtapaProcesoProduccionLinea",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductoComponente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadMedidaSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CantidadPorUnidad = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoComponente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductoComponente_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductoComponente_ProductoId",
                table: "ProductoComponente",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductoComponente");

            migrationBuilder.DropColumn(
                name: "CantidadBasePorUnidad",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "ProductoBaseId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "ProductoBaseNombre",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "LoteNumero",
                table: "ProcesoProduccionLineaInicial");

            migrationBuilder.DropColumn(
                name: "LoteProcesoId",
                table: "ProcesoProduccionLineaInicial");

            migrationBuilder.DropColumn(
                name: "LoteNumero",
                table: "EtapaProcesoProduccionLinea");

            migrationBuilder.DropColumn(
                name: "LoteProcesoId",
                table: "EtapaProcesoProduccionLinea");
        }
    }
}
