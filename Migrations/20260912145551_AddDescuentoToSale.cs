using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace panialera.Migrations
{
    /// <inheritdoc />
    public partial class AddDescuentoToSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescuentoDescripcion",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DescuentoEsPorcentaje",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DescuentoMonto",
                table: "Sales",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DescuentoValor",
                table: "Sales",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescuentoDescripcion",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DescuentoEsPorcentaje",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DescuentoMonto",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DescuentoValor",
                table: "Sales");
        }
    }
}
