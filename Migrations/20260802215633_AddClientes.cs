using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace panialera.Migrations
{
    /// <inheritdoc />
    public partial class AddClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "Sales",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", nullable: true),
                    Direccion = table.Column<string>(type: "TEXT", nullable: true),
                    Extras = table.Column<string>(type: "TEXT", nullable: true),
                    Opcional1 = table.Column<string>(type: "TEXT", nullable: true),
                    Opcional2 = table.Column<string>(type: "TEXT", nullable: true),
                    Opcional3 = table.Column<string>(type: "TEXT", nullable: true),
                    Opcional4 = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ClienteId",
                table: "Sales",
                column: "ClienteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Clientes_ClienteId",
                table: "Sales",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Clientes_ClienteId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Sales_ClienteId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Sales");
        }
    }
}
