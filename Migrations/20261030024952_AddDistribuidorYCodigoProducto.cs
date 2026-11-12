using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace panialera.Migrations
{
    /// <inheritdoc />
    public partial class AddDistribuidorYCodigoProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagenArchivo",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DistribuidorProductos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Categoria = table.Column<string>(type: "TEXT", nullable: true),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: true),
                    PrecioWeb = table.Column<decimal>(type: "TEXT", nullable: true),
                    ConStock = table.Column<bool>(type: "INTEGER", nullable: false),
                    Alerta = table.Column<string>(type: "TEXT", nullable: true),
                    ImagenArchivo = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistribuidorProductos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DistribuidorSyncs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Generado = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportadoAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistribuidorSyncs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Codigo",
                table: "Products",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_DistribuidorProductos_Codigo",
                table: "DistribuidorProductos",
                column: "Codigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistribuidorProductos");

            migrationBuilder.DropTable(
                name: "DistribuidorSyncs");

            migrationBuilder.DropIndex(
                name: "IX_Products_Codigo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ImagenArchivo",
                table: "Products");
        }
    }
}
