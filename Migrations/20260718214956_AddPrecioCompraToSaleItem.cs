using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace panialera.Migrations
{
    /// <inheritdoc />
    public partial class AddPrecioCompraToSaleItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecioCompra",
                table: "SaleItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            // Best-effort backfill: for existing sales, take the current purchase price
            // of the matching product (if it still exists).
            migrationBuilder.Sql(@"
                UPDATE SaleItems
                SET PrecioCompra = (
                    SELECT p.PrecioCompra FROM Products p WHERE p.Id = SaleItems.ProductId
                )
                WHERE PrecioCompra = 0
                  AND EXISTS (SELECT 1 FROM Products p WHERE p.Id = SaleItems.ProductId);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecioCompra",
                table: "SaleItems");
        }
    }
}
