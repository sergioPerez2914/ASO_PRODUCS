using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProcesos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcesoProduccionId",
                table: "SalidasMateriaPrima",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcesoProduccionNumero",
                table: "SalidasMateriaPrima",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProcesoProduccionId",
                table: "SalidasInventario",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcesoProduccionNumero",
                table: "SalidasInventario",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Despachos",
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
                    table.PrimaryKey("PK_Despachos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EtapasProduccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtapasProduccion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcesosProduccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ProductoNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadMedidaSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CantidadPlaneada = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CantidadProducida = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    CreadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TerminadoPorId = table.Column<int>(type: "int", nullable: true),
                    TerminadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaTermino = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcesosProduccion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productos",
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
                    table.PrimaryKey("PK_Productos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DespachoLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    ProductoNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadMedidaSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DespachoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DespachoLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DespachoLinea_Despachos_DespachoId",
                        column: x => x.DespachoId,
                        principalTable: "Despachos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EtapaProcesoProduccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtapaProduccionId = table.Column<int>(type: "int", nullable: false),
                    EtapaProduccionNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcesoProduccionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtapaProcesoProduccion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EtapaProcesoProduccion_ProcesosProduccion_ProcesoProduccionId",
                        column: x => x.ProcesoProduccionId,
                        principalTable: "ProcesosProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcesoProduccionLineaInicial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadMedidaSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProcesoProduccionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcesoProduccionLineaInicial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcesoProduccionLineaInicial_ProcesosProduccion_ProcesoProduccionId",
                        column: x => x.ProcesoProduccionId,
                        principalTable: "ProcesosProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EtapaProcesoProduccionLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnidadMedidaSnapshot = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EtapaProcesoProduccionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtapaProcesoProduccionLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EtapaProcesoProduccionLinea_EtapaProcesoProduccion_EtapaProcesoProduccionId",
                        column: x => x.EtapaProcesoProduccionId,
                        principalTable: "EtapaProcesoProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalidasMateriaPrima_ProcesoProduccionId",
                table: "SalidasMateriaPrima",
                column: "ProcesoProduccionId");

            migrationBuilder.CreateIndex(
                name: "IX_SalidasInventario_ProcesoProduccionId",
                table: "SalidasInventario",
                column: "ProcesoProduccionId");

            migrationBuilder.CreateIndex(
                name: "IX_DespachoLinea_DespachoId",
                table: "DespachoLinea",
                column: "DespachoId");

            migrationBuilder.CreateIndex(
                name: "IX_Despachos_OrganizacionId_Numero",
                table: "Despachos",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtapaProcesoProduccion_ProcesoProduccionId",
                table: "EtapaProcesoProduccion",
                column: "ProcesoProduccionId");

            migrationBuilder.CreateIndex(
                name: "IX_EtapaProcesoProduccionLinea_EtapaProcesoProduccionId",
                table: "EtapaProcesoProduccionLinea",
                column: "EtapaProcesoProduccionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcesoProduccionLineaInicial_ProcesoProduccionId",
                table: "ProcesoProduccionLineaInicial",
                column: "ProcesoProduccionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcesosProduccion_OrganizacionId_Numero",
                table: "ProcesosProduccion",
                columns: new[] { "OrganizacionId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DespachoLinea");

            migrationBuilder.DropTable(
                name: "EtapaProcesoProduccionLinea");

            migrationBuilder.DropTable(
                name: "EtapasProduccion");

            migrationBuilder.DropTable(
                name: "ProcesoProduccionLineaInicial");

            migrationBuilder.DropTable(
                name: "Productos");

            migrationBuilder.DropTable(
                name: "Despachos");

            migrationBuilder.DropTable(
                name: "EtapaProcesoProduccion");

            migrationBuilder.DropTable(
                name: "ProcesosProduccion");

            migrationBuilder.DropIndex(
                name: "IX_SalidasMateriaPrima_ProcesoProduccionId",
                table: "SalidasMateriaPrima");

            migrationBuilder.DropIndex(
                name: "IX_SalidasInventario_ProcesoProduccionId",
                table: "SalidasInventario");

            migrationBuilder.DropColumn(
                name: "ProcesoProduccionId",
                table: "SalidasMateriaPrima");

            migrationBuilder.DropColumn(
                name: "ProcesoProduccionNumero",
                table: "SalidasMateriaPrima");

            migrationBuilder.DropColumn(
                name: "ProcesoProduccionId",
                table: "SalidasInventario");

            migrationBuilder.DropColumn(
                name: "ProcesoProduccionNumero",
                table: "SalidasInventario");
        }
    }
}
