using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ASO_PRODUCS.Desktop.Migrations
{
    /// <inheritdoc />
    public partial class AgregarLotesYVencimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasVidaUtil",
                table: "Productos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVencimiento",
                table: "ProcesosProduccion",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasVidaUtil",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "FechaVencimiento",
                table: "ProcesosProduccion");
        }
    }
}
