using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tenis3t.Migrations
{
    /// <inheritdoc />
    public partial class AddFechaDevolucionToPrestamoAddFechaDevolucionToPrestamo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaDevolucion",
                table: "Prestamos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.InsertData(
                table: "MetodoPagos",
                columns: new[] { "Id", "Nombre" },
                values: new object[,]
                {
                    { 1, "Efectivo" },
                    { 2, "Crédito" },
                    { 3, "Transferencia" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MetodoPagos",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MetodoPagos",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MetodoPagos",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "FechaDevolucion",
                table: "Prestamos");
        }
    }
}
