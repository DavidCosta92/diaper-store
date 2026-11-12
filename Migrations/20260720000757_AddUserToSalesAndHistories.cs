using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace panialera.Migrations
{
    /// <inheritdoc />
    public partial class AddUserToSalesAndHistories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Sales",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastUpdatedByUserId",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: false),
                    NewValue = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductChanges_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductChanges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductPriceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PrecioCompra = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_UserId",
                table: "Sales",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_LastUpdatedByUserId",
                table: "Products",
                column: "LastUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductChanges_ProductId",
                table: "ProductChanges",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductChanges_UserId",
                table: "ProductChanges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceHistories_ProductId",
                table: "ProductPriceHistories",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_LastUpdatedByUserId",
                table: "Products",
                column: "LastUpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Users_UserId",
                table: "Sales",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Users_LastUpdatedByUserId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Users_UserId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "ProductChanges");

            migrationBuilder.DropTable(
                name: "ProductPriceHistories");

            migrationBuilder.DropIndex(
                name: "IX_Sales_UserId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Products_LastUpdatedByUserId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LastUpdatedByUserId",
                table: "Products");
        }
    }
}
