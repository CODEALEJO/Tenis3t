using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenis3t.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRelationsToPrestamo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prestamos_AspNetUsers_UsuarioId",
                table: "Prestamos");

            migrationBuilder.RenameColumn(
                name: "UsuarioId",
                table: "Prestamos",
                newName: "UsuarioReceptorId");

            migrationBuilder.RenameIndex(
                name: "IX_Prestamos_UsuarioId",
                table: "Prestamos",
                newName: "IX_Prestamos_UsuarioReceptorId");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioPrestamistaId",
                table: "Prestamos",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Prestamos_UsuarioPrestamistaId",
                table: "Prestamos",
                column: "UsuarioPrestamistaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamos_AspNetUsers_UsuarioPrestamistaId",
                table: "Prestamos",
                column: "UsuarioPrestamistaId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamos_AspNetUsers_UsuarioReceptorId",
                table: "Prestamos",
                column: "UsuarioReceptorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prestamos_AspNetUsers_UsuarioPrestamistaId",
                table: "Prestamos");

            migrationBuilder.DropForeignKey(
                name: "FK_Prestamos_AspNetUsers_UsuarioReceptorId",
                table: "Prestamos");

            migrationBuilder.DropIndex(
                name: "IX_Prestamos_UsuarioPrestamistaId",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "UsuarioPrestamistaId",
                table: "Prestamos");

            migrationBuilder.RenameColumn(
                name: "UsuarioReceptorId",
                table: "Prestamos",
                newName: "UsuarioId");

            migrationBuilder.RenameIndex(
                name: "IX_Prestamos_UsuarioReceptorId",
                table: "Prestamos",
                newName: "IX_Prestamos_UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamos_AspNetUsers_UsuarioId",
                table: "Prestamos",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
