using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenis3t.Migrations
{
    /// <inheritdoc />
    public partial class modoficaciondatelleventa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventarioId",
                table: "DetallesVenta",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventarioId",
                table: "DetallesVenta");
        }
    }
}
