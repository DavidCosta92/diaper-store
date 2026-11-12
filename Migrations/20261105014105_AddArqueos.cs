using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace panialera.Migrations
{
    /// <inheritdoc />
    public partial class AddArqueos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Arqueos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IniciadoAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IniciadoPor = table.Column<string>(type: "TEXT", nullable: true),
                    FinalizadoAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinalizadoPor = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arqueos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArqueoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArqueoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", nullable: true),
                    ProductName = table.Column<string>(type: "TEXT", nullable: false),
                    ProductDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: true),
                    StockSistema = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioCompra = table.Column<decimal>(type: "TEXT", nullable: false),
                    Verificado = table.Column<bool>(type: "INTEGER", nullable: false),
                    Diferencia = table.Column<int>(type: "INTEGER", nullable: false),
                    Nota = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArqueoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArqueoItems_Arqueos_ArqueoId",
                        column: x => x.ArqueoId,
                        principalTable: "Arqueos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArqueoItems_ArqueoId",
                table: "ArqueoItems",
                column: "ArqueoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArqueoItems");

            migrationBuilder.DropTable(
                name: "Arqueos");
        }
    }
}
