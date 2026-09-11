using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMateriaPrima : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecepcionesMateriaPrima",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    CreadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecepcionesMateriaPrima", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalidasMateriaPrima",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutorizadoPorId = table.Column<int>(type: "int", nullable: false),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalidasMateriaPrima", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TiposMateriaPrima",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadMedida = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposMateriaPrima", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecepcionMateriaPrimaLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoMateriaPrimaId = table.Column<int>(type: "int", nullable: false),
                    TipoMateriaPrimaNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadMedidaSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RecepcionMateriaPrimaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecepcionMateriaPrimaLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecepcionMateriaPrimaLinea_RecepcionesMateriaPrima_RecepcionMateriaPrimaId",
                        column: x => x.RecepcionMateriaPrimaId,
                        principalTable: "RecepcionesMateriaPrima",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalidaMateriaPrimaLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoMateriaPrimaId = table.Column<int>(type: "int", nullable: false),
                    TipoMateriaPrimaNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadMedidaSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalidaMateriaPrimaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalidaMateriaPrimaLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalidaMateriaPrimaLinea_SalidasMateriaPrima_SalidaMateriaPrimaId",
                        column: x => x.SalidaMateriaPrimaId,
                        principalTable: "SalidasMateriaPrima",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionesMateriaPrima_OrganizacionId_Numero",
                table: "RecepcionesMateriaPrima",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecepcionMateriaPrimaLinea_RecepcionMateriaPrimaId",
                table: "RecepcionMateriaPrimaLinea",
                column: "RecepcionMateriaPrimaId");

            migrationBuilder.CreateIndex(
                name: "IX_SalidaMateriaPrimaLinea_SalidaMateriaPrimaId",
                table: "SalidaMateriaPrimaLinea",
                column: "SalidaMateriaPrimaId");

            migrationBuilder.CreateIndex(
                name: "IX_SalidasMateriaPrima_OrganizacionId_Numero",
                table: "SalidasMateriaPrima",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecepcionMateriaPrimaLinea");

            migrationBuilder.DropTable(
                name: "SalidaMateriaPrimaLinea");

            migrationBuilder.DropTable(
                name: "TiposMateriaPrima");

            migrationBuilder.DropTable(
                name: "RecepcionesMateriaPrima");

            migrationBuilder.DropTable(
                name: "SalidasMateriaPrima");
        }
    }
}
