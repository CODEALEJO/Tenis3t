using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenis3t.Migrations
{
    /// <inheritdoc />
    public partial class FixPrestamosTableStructure : Migration
    {
        /// <inheritdoc />
     protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1. Primero eliminar la constraint de clave foránea
    migrationBuilder.DropForeignKey(
        name: "FK_Prestamos_AspNetUsers_UsuarioReceptorId",
        table: "Prestamos");

    // 2. Modificar la columna para permitir NULL
    migrationBuilder.AlterColumn<string>(
        name: "UsuarioReceptorId",
        table: "Prestamos",
        nullable: true,  // Permitir NULL
        oldClrType: typeof(string),
        oldType: "varchar(255)");

    // 3. Modificar FechaDevolucionEstimada para permitir NULL
    migrationBuilder.AlterColumn<DateTime>(
        name: "FechaDevolucionEstimada",
        table: "Prestamos",
        nullable: true,  // Permitir NULL
        oldClrType: typeof(DateTime),
        oldType: "datetime");

    // 4. Volver a agregar la constraint de clave foránea
    migrationBuilder.AddForeignKey(
        name: "FK_Prestamos_AspNetUsers_UsuarioReceptorId",
        table: "Prestamos",
        column: "UsuarioReceptorId",
        principalTable: "AspNetUsers",
        principalColumn: "Id",
        onDelete: ReferentialAction.Restrict);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Código para revertir los cambios si es necesario
    migrationBuilder.DropForeignKey(
        name: "FK_Prestamos_AspNetUsers_UsuarioReceptorId",
        table: "Prestamos");

    migrationBuilder.AlterColumn<string>(
        name: "UsuarioReceptorId",
        table: "Prestamos",
        type: "varchar(255)",
        nullable: false,
        oldClrType: typeof(string),
        oldNullable: true);

    migrationBuilder.AlterColumn<DateTime>(
        name: "FechaDevolucionEstimada",
        table: "Prestamos",
        type: "datetime",
        nullable: false,
        oldClrType: typeof(DateTime),
        oldNullable: true);

    migrationBuilder.AddForeignKey(
        name: "FK_Prestamos_AspNetUsers_UsuarioReceptorId",
        table: "Prestamos",
        column: "UsuarioReceptorId",
        principalTable: "AspNetUsers",
        principalColumn: "Id",
        onDelete: ReferentialAction.Cascade);
}
    }
}
