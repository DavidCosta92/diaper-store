using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace panialera.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", nullable: true),
                    Categoria = table.Column<string>(type: "TEXT", nullable: false),
                    Accion = table.Column<string>(type: "TEXT", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: false),
                    Detalle = table.Column<string>(type: "TEXT", nullable: true),
                    EntityType = table.Column<string>(type: "TEXT", nullable: true),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Fecha",
                table: "AuditLogs",
                column: "Fecha");

            // Move the existing product change history into the audit log
            // (before dropping the old table, so no data is lost).
            // "Cantidad" (stock) changes are recorded as Stock / Ajuste manual.
            migrationBuilder.Sql(@"
                INSERT INTO AuditLogs (Fecha, UserId, UserName, Categoria, Accion, Descripcion, Detalle, EntityType, EntityId)
                SELECT pc.ChangedAt,
                       pc.UserId,
                       u.Nombre,
                       CASE WHEN pc.FieldName = 'Cantidad' THEN 'Stock' ELSE 'Productos' END,
                       CASE WHEN pc.FieldName = 'Cantidad' THEN 'Ajuste manual' ELSE 'Editar' END,
                       CASE WHEN pc.FieldName = 'Cantidad'
                            THEN 'Ajuste de stock de ' || COALESCE(p.Nombre, 'producto #' || pc.ProductId)
                            ELSE 'Se editó el producto ' || COALESCE(p.Nombre, '#' || pc.ProductId) END,
                       pc.FieldName || ': ' || COALESCE(NULLIF(pc.OldValue, ''), '(vacío)') || ' → ' || COALESCE(NULLIF(pc.NewValue, ''), '(vacío)'),
                       'Producto',
                       pc.ProductId
                FROM ProductChanges pc
                LEFT JOIN Products p ON p.Id = pc.ProductId
                LEFT JOIN Users u ON u.Id = pc.UserId;");

            migrationBuilder.DropTable(
                name: "ProductChanges");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

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
                    NewValue = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_ProductChanges_ProductId",
                table: "ProductChanges",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductChanges_UserId",
                table: "ProductChanges",
                column: "UserId");
        }
    }
}
