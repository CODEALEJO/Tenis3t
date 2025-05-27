using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenis3t.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioToInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Inventarios",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Inventarios_UsuarioId",
                table: "Inventarios",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventarios_AspNetUsers_UsuarioId",
                table: "Inventarios",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventarios_AspNetUsers_UsuarioId",
                table: "Inventarios");

            migrationBuilder.DropIndex(
                name: "IX_Inventarios_UsuarioId",
                table: "Inventarios");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Inventarios");
        }
    }
}
