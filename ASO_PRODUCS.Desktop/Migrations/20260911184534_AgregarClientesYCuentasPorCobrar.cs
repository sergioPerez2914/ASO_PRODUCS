using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarClientesYCuentasPorCobrar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "Despachos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClienteNombre",
                table: "Despachos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FacturaClienteId",
                table: "Despachos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacturaClienteNumero",
                table: "Despachos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Despachos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Despachos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioUnitario",
                table: "DespachoLinea",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "DespachoLinea",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Rif = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacturasCliente",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizacionId = table.Column<int>(type: "int", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    ClienteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCobro = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DespachoId = table.Column<int>(type: "int", nullable: true),
                    DespachoNumero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturasCliente", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacturaClienteLinea",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestinoTexto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CantidadTexto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FacturaClienteId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacturaClienteLinea", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacturaClienteLinea_FacturasCliente_FacturaClienteId",
                        column: x => x.FacturaClienteId,
                        principalTable: "FacturasCliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Despachos_FacturaClienteId",
                table: "Despachos",
                column: "FacturaClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturaClienteLinea_FacturaClienteId",
                table: "FacturaClienteLinea",
                column: "FacturaClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasCliente_DespachoId",
                table: "FacturasCliente",
                column: "DespachoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "FacturaClienteLinea");

            migrationBuilder.DropTable(
                name: "FacturasCliente");

            migrationBuilder.DropIndex(
                name: "IX_Despachos_FacturaClienteId",
                table: "Despachos");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Despachos");

            migrationBuilder.DropColumn(
                name: "ClienteNombre",
                table: "Despachos");

            migrationBuilder.DropColumn(
                name: "FacturaClienteId",
                table: "Despachos");

            migrationBuilder.DropColumn(
                name: "FacturaClienteNumero",
                table: "Despachos");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Despachos");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Despachos");

            migrationBuilder.DropColumn(
                name: "PrecioUnitario",
                table: "DespachoLinea");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "DespachoLinea");
        }
    }
}
