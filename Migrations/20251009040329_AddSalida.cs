using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenis3t.Migrations
{
    /// <inheritdoc />
    public partial class AddSalida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Salidas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    InventarioId = table.Column<int>(type: "int", nullable: false),
                    TallaInventarioId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FechaSalida = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Salidas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Salidas_Inventarios_InventarioId",
                        column: x => x.InventarioId,
                        principalTable: "Inventarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Salidas_TallasInventario_TallaInventarioId",
                        column: x => x.TallaInventarioId,
                        principalTable: "TallasInventario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Salidas_InventarioId",
                table: "Salidas",
                column: "InventarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Salidas_TallaInventarioId",
                table: "Salidas",
                column: "TallaInventarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Salidas");
        }
    }
}
