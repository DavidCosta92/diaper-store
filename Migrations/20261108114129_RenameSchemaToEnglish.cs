using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace panialera.Migrations
{
    /// <summary>
    /// Renames tables, columns and indexes from Spanish to English, keeping all the data
    /// (written by hand: the scaffolder guessed the renames wrong and would have dropped
    /// tables). It also rewrites the stored audit categories and record types.
    /// </summary>
    public partial class RenameSchemaToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Indexes are dropped first and recreated with their new names at the end.
            migrationBuilder.DropIndex(name: "IX_Products_Codigo", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Sales_ClienteId", table: "Sales");
            migrationBuilder.DropIndex(name: "IX_Users_Nombre", table: "Users");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Fecha", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_Tareas_ClienteId", table: "Tareas");
            migrationBuilder.DropIndex(name: "IX_DistribuidorProductos_Codigo", table: "DistribuidorProductos");
            migrationBuilder.DropIndex(name: "IX_ArqueoItems_ArqueoId", table: "ArqueoItems");

            // Tables. SQLite updates the foreign keys that point to a renamed table.
            migrationBuilder.RenameTable(name: "Clientes", newName: "Customers");
            migrationBuilder.RenameTable(name: "Tareas", newName: "TodoItems");
            migrationBuilder.RenameTable(name: "Egresos", newName: "Expenses");
            migrationBuilder.RenameTable(name: "DistribuidorProductos", newName: "DistributorProducts");
            migrationBuilder.RenameTable(name: "DistribuidorSyncs", newName: "DistributorSyncs");
            migrationBuilder.RenameTable(name: "Arqueos", newName: "StockCounts");
            migrationBuilder.RenameTable(name: "ArqueoItems", newName: "StockCountItems");

            // Columns.
            migrationBuilder.RenameColumn(name: "Codigo", table: "Products", newName: "Code");
            migrationBuilder.RenameColumn(name: "Nombre", table: "Products", newName: "Name");
            migrationBuilder.RenameColumn(name: "Categoria", table: "Products", newName: "Category");
            migrationBuilder.RenameColumn(name: "Descripcion", table: "Products", newName: "Description");
            migrationBuilder.RenameColumn(name: "Cantidad", table: "Products", newName: "Stock");
            migrationBuilder.RenameColumn(name: "PrecioCompra", table: "Products", newName: "PurchasePrice");
            migrationBuilder.RenameColumn(name: "PrecioVenta", table: "Products", newName: "SalePrice");
            migrationBuilder.RenameColumn(name: "StockMinimo", table: "Products", newName: "MinStock");
            migrationBuilder.RenameColumn(name: "ImagenArchivo", table: "Products", newName: "ImageFile");
            migrationBuilder.RenameColumn(name: "Fecha", table: "Sales", newName: "Date");
            migrationBuilder.RenameColumn(name: "Anulada", table: "Sales", newName: "Voided");
            migrationBuilder.RenameColumn(name: "ClienteId", table: "Sales", newName: "CustomerId");
            migrationBuilder.RenameColumn(name: "DescuentoValor", table: "Sales", newName: "DiscountValue");
            migrationBuilder.RenameColumn(name: "DescuentoEsPorcentaje", table: "Sales", newName: "DiscountIsPercentage");
            migrationBuilder.RenameColumn(name: "DescuentoMonto", table: "Sales", newName: "DiscountAmount");
            migrationBuilder.RenameColumn(name: "DescuentoDescripcion", table: "Sales", newName: "DiscountReason");
            migrationBuilder.RenameColumn(name: "Cantidad", table: "SaleItems", newName: "Quantity");
            migrationBuilder.RenameColumn(name: "PrecioVenta", table: "SaleItems", newName: "SalePrice");
            migrationBuilder.RenameColumn(name: "PrecioCompra", table: "SaleItems", newName: "PurchasePrice");
            migrationBuilder.RenameColumn(name: "Nombre", table: "Users", newName: "Name");
            migrationBuilder.RenameColumn(name: "PrecioCompra", table: "ProductPriceHistories", newName: "PurchasePrice");
            migrationBuilder.RenameColumn(name: "PrecioVenta", table: "ProductPriceHistories", newName: "SalePrice");
            migrationBuilder.RenameColumn(name: "Fecha", table: "AuditLogs", newName: "Date");
            migrationBuilder.RenameColumn(name: "Categoria", table: "AuditLogs", newName: "Category");
            migrationBuilder.RenameColumn(name: "Accion", table: "AuditLogs", newName: "Action");
            migrationBuilder.RenameColumn(name: "Descripcion", table: "AuditLogs", newName: "Description");
            migrationBuilder.RenameColumn(name: "Detalle", table: "AuditLogs", newName: "Details");
            migrationBuilder.RenameColumn(name: "Nombre", table: "Customers", newName: "Name");
            migrationBuilder.RenameColumn(name: "Telefono", table: "Customers", newName: "Phone");
            migrationBuilder.RenameColumn(name: "Direccion", table: "Customers", newName: "Address");
            migrationBuilder.RenameColumn(name: "Opcional1", table: "Customers", newName: "Optional1");
            migrationBuilder.RenameColumn(name: "Opcional2", table: "Customers", newName: "Optional2");
            migrationBuilder.RenameColumn(name: "Opcional3", table: "Customers", newName: "Optional3");
            migrationBuilder.RenameColumn(name: "Opcional4", table: "Customers", newName: "Optional4");
            migrationBuilder.RenameColumn(name: "Titulo", table: "TodoItems", newName: "Title");
            migrationBuilder.RenameColumn(name: "Descripcion", table: "TodoItems", newName: "Description");
            migrationBuilder.RenameColumn(name: "Prioridad", table: "TodoItems", newName: "Priority");
            migrationBuilder.RenameColumn(name: "Completada", table: "TodoItems", newName: "Completed");
            migrationBuilder.RenameColumn(name: "CompletadaAt", table: "TodoItems", newName: "CompletedAt");
            migrationBuilder.RenameColumn(name: "ClienteId", table: "TodoItems", newName: "CustomerId");
            migrationBuilder.RenameColumn(name: "Nombre", table: "Expenses", newName: "Name");
            migrationBuilder.RenameColumn(name: "Descripcion", table: "Expenses", newName: "Description");
            migrationBuilder.RenameColumn(name: "Valor", table: "Expenses", newName: "Amount");
            migrationBuilder.RenameColumn(name: "Fecha", table: "Expenses", newName: "Date");
            migrationBuilder.RenameColumn(name: "TipoPago", table: "Expenses", newName: "PaymentType");
            migrationBuilder.RenameColumn(name: "Codigo", table: "DistributorProducts", newName: "Code");
            migrationBuilder.RenameColumn(name: "Nombre", table: "DistributorProducts", newName: "Name");
            migrationBuilder.RenameColumn(name: "Categoria", table: "DistributorProducts", newName: "Category");
            migrationBuilder.RenameColumn(name: "Descripcion", table: "DistributorProducts", newName: "Description");
            migrationBuilder.RenameColumn(name: "PrecioWeb", table: "DistributorProducts", newName: "WebPrice");
            migrationBuilder.RenameColumn(name: "ConStock", table: "DistributorProducts", newName: "InStock");
            migrationBuilder.RenameColumn(name: "Alerta", table: "DistributorProducts", newName: "Alert");
            migrationBuilder.RenameColumn(name: "ImagenArchivo", table: "DistributorProducts", newName: "ImageFile");
            migrationBuilder.RenameColumn(name: "Generado", table: "DistributorSyncs", newName: "GeneratedAt");
            migrationBuilder.RenameColumn(name: "ImportadoAt", table: "DistributorSyncs", newName: "ImportedAt");
            migrationBuilder.RenameColumn(name: "Cantidad", table: "DistributorSyncs", newName: "Count");
            migrationBuilder.RenameColumn(name: "IniciadoAt", table: "StockCounts", newName: "StartedAt");
            migrationBuilder.RenameColumn(name: "IniciadoPor", table: "StockCounts", newName: "StartedBy");
            migrationBuilder.RenameColumn(name: "FinalizadoAt", table: "StockCounts", newName: "FinishedAt");
            migrationBuilder.RenameColumn(name: "FinalizadoPor", table: "StockCounts", newName: "FinishedBy");
            migrationBuilder.RenameColumn(name: "ArqueoId", table: "StockCountItems", newName: "StockCountId");
            migrationBuilder.RenameColumn(name: "Codigo", table: "StockCountItems", newName: "Code");
            migrationBuilder.RenameColumn(name: "Categoria", table: "StockCountItems", newName: "Category");
            migrationBuilder.RenameColumn(name: "StockSistema", table: "StockCountItems", newName: "SystemStock");
            migrationBuilder.RenameColumn(name: "PrecioCompra", table: "StockCountItems", newName: "PurchasePrice");
            migrationBuilder.RenameColumn(name: "Verificado", table: "StockCountItems", newName: "Verified");
            migrationBuilder.RenameColumn(name: "Diferencia", table: "StockCountItems", newName: "Difference");
            migrationBuilder.RenameColumn(name: "Nota", table: "StockCountItems", newName: "Note");

            migrationBuilder.CreateIndex(name: "IX_Products_Code", table: "Products", column: "Code");
            migrationBuilder.CreateIndex(name: "IX_Sales_CustomerId", table: "Sales", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_Users_Name", table: "Users", column: "Name", unique: true);
            migrationBuilder.CreateIndex(name: "IX_AuditLogs_Date", table: "AuditLogs", column: "Date");
            migrationBuilder.CreateIndex(name: "IX_TodoItems_CustomerId", table: "TodoItems", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_DistributorProducts_Code", table: "DistributorProducts", column: "Code");
            migrationBuilder.CreateIndex(name: "IX_StockCountItems_StockCountId", table: "StockCountItems", column: "StockCountId");

            // Stored values: audit categories (enum stored as text) and record types.
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Products' WHERE Category = 'Productos';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Sales' WHERE Category = 'Ventas';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Customers' WHERE Category = 'Clientes';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Expenses' WHERE Category = 'Egresos';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Todos' WHERE Category = 'Pendientes';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Users' WHERE Category = 'Usuarios';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Distributor' WHERE Category = 'Distribuidor';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'StockCounts' WHERE Category = 'Arqueos';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Session' WHERE Category = 'Sesion';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'System' WHERE Category = 'Sistema';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Errors' WHERE Category = 'Errores';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Product' WHERE EntityType = 'Producto';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Sale' WHERE EntityType = 'Venta';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Customer' WHERE EntityType = 'Cliente';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Expense' WHERE EntityType = 'Egreso';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'TodoItem' WHERE EntityType = 'Tarea';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'User' WHERE EntityType = 'Usuario';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'StockCount' WHERE EntityType = 'Arqueo';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Products_Code", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Sales_CustomerId", table: "Sales");
            migrationBuilder.DropIndex(name: "IX_Users_Name", table: "Users");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Date", table: "AuditLogs");
            migrationBuilder.DropIndex(name: "IX_TodoItems_CustomerId", table: "TodoItems");
            migrationBuilder.DropIndex(name: "IX_DistributorProducts_Code", table: "DistributorProducts");
            migrationBuilder.DropIndex(name: "IX_StockCountItems_StockCountId", table: "StockCountItems");

            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Productos' WHERE Category = 'Products';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Ventas' WHERE Category = 'Sales';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Clientes' WHERE Category = 'Customers';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Egresos' WHERE Category = 'Expenses';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Pendientes' WHERE Category = 'Todos';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Usuarios' WHERE Category = 'Users';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Distribuidor' WHERE Category = 'Distributor';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Arqueos' WHERE Category = 'StockCounts';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Sesion' WHERE Category = 'Session';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Sistema' WHERE Category = 'System';");
            migrationBuilder.Sql("UPDATE AuditLogs SET Category = 'Errores' WHERE Category = 'Errors';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Producto' WHERE EntityType = 'Product';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Venta' WHERE EntityType = 'Sale';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Cliente' WHERE EntityType = 'Customer';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Egreso' WHERE EntityType = 'Expense';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Tarea' WHERE EntityType = 'TodoItem';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Usuario' WHERE EntityType = 'User';");
            migrationBuilder.Sql("UPDATE AuditLogs SET EntityType = 'Arqueo' WHERE EntityType = 'StockCount';");

            migrationBuilder.RenameColumn(name: "Code", table: "Products", newName: "Codigo");
            migrationBuilder.RenameColumn(name: "Name", table: "Products", newName: "Nombre");
            migrationBuilder.RenameColumn(name: "Category", table: "Products", newName: "Categoria");
            migrationBuilder.RenameColumn(name: "Description", table: "Products", newName: "Descripcion");
            migrationBuilder.RenameColumn(name: "Stock", table: "Products", newName: "Cantidad");
            migrationBuilder.RenameColumn(name: "PurchasePrice", table: "Products", newName: "PrecioCompra");
            migrationBuilder.RenameColumn(name: "SalePrice", table: "Products", newName: "PrecioVenta");
            migrationBuilder.RenameColumn(name: "MinStock", table: "Products", newName: "StockMinimo");
            migrationBuilder.RenameColumn(name: "ImageFile", table: "Products", newName: "ImagenArchivo");
            migrationBuilder.RenameColumn(name: "Date", table: "Sales", newName: "Fecha");
            migrationBuilder.RenameColumn(name: "Voided", table: "Sales", newName: "Anulada");
            migrationBuilder.RenameColumn(name: "CustomerId", table: "Sales", newName: "ClienteId");
            migrationBuilder.RenameColumn(name: "DiscountValue", table: "Sales", newName: "DescuentoValor");
            migrationBuilder.RenameColumn(name: "DiscountIsPercentage", table: "Sales", newName: "DescuentoEsPorcentaje");
            migrationBuilder.RenameColumn(name: "DiscountAmount", table: "Sales", newName: "DescuentoMonto");
            migrationBuilder.RenameColumn(name: "DiscountReason", table: "Sales", newName: "DescuentoDescripcion");
            migrationBuilder.RenameColumn(name: "Quantity", table: "SaleItems", newName: "Cantidad");
            migrationBuilder.RenameColumn(name: "SalePrice", table: "SaleItems", newName: "PrecioVenta");
            migrationBuilder.RenameColumn(name: "PurchasePrice", table: "SaleItems", newName: "PrecioCompra");
            migrationBuilder.RenameColumn(name: "Name", table: "Users", newName: "Nombre");
            migrationBuilder.RenameColumn(name: "PurchasePrice", table: "ProductPriceHistories", newName: "PrecioCompra");
            migrationBuilder.RenameColumn(name: "SalePrice", table: "ProductPriceHistories", newName: "PrecioVenta");
            migrationBuilder.RenameColumn(name: "Date", table: "AuditLogs", newName: "Fecha");
            migrationBuilder.RenameColumn(name: "Category", table: "AuditLogs", newName: "Categoria");
            migrationBuilder.RenameColumn(name: "Action", table: "AuditLogs", newName: "Accion");
            migrationBuilder.RenameColumn(name: "Description", table: "AuditLogs", newName: "Descripcion");
            migrationBuilder.RenameColumn(name: "Details", table: "AuditLogs", newName: "Detalle");
            migrationBuilder.RenameColumn(name: "Name", table: "Customers", newName: "Nombre");
            migrationBuilder.RenameColumn(name: "Phone", table: "Customers", newName: "Telefono");
            migrationBuilder.RenameColumn(name: "Address", table: "Customers", newName: "Direccion");
            migrationBuilder.RenameColumn(name: "Optional1", table: "Customers", newName: "Opcional1");
            migrationBuilder.RenameColumn(name: "Optional2", table: "Customers", newName: "Opcional2");
            migrationBuilder.RenameColumn(name: "Optional3", table: "Customers", newName: "Opcional3");
            migrationBuilder.RenameColumn(name: "Optional4", table: "Customers", newName: "Opcional4");
            migrationBuilder.RenameColumn(name: "Title", table: "TodoItems", newName: "Titulo");
            migrationBuilder.RenameColumn(name: "Description", table: "TodoItems", newName: "Descripcion");
            migrationBuilder.RenameColumn(name: "Priority", table: "TodoItems", newName: "Prioridad");
            migrationBuilder.RenameColumn(name: "Completed", table: "TodoItems", newName: "Completada");
            migrationBuilder.RenameColumn(name: "CompletedAt", table: "TodoItems", newName: "CompletadaAt");
            migrationBuilder.RenameColumn(name: "CustomerId", table: "TodoItems", newName: "ClienteId");
            migrationBuilder.RenameColumn(name: "Name", table: "Expenses", newName: "Nombre");
            migrationBuilder.RenameColumn(name: "Description", table: "Expenses", newName: "Descripcion");
            migrationBuilder.RenameColumn(name: "Amount", table: "Expenses", newName: "Valor");
            migrationBuilder.RenameColumn(name: "Date", table: "Expenses", newName: "Fecha");
            migrationBuilder.RenameColumn(name: "PaymentType", table: "Expenses", newName: "TipoPago");
            migrationBuilder.RenameColumn(name: "Code", table: "DistributorProducts", newName: "Codigo");
            migrationBuilder.RenameColumn(name: "Name", table: "DistributorProducts", newName: "Nombre");
            migrationBuilder.RenameColumn(name: "Category", table: "DistributorProducts", newName: "Categoria");
            migrationBuilder.RenameColumn(name: "Description", table: "DistributorProducts", newName: "Descripcion");
            migrationBuilder.RenameColumn(name: "WebPrice", table: "DistributorProducts", newName: "PrecioWeb");
            migrationBuilder.RenameColumn(name: "InStock", table: "DistributorProducts", newName: "ConStock");
            migrationBuilder.RenameColumn(name: "Alert", table: "DistributorProducts", newName: "Alerta");
            migrationBuilder.RenameColumn(name: "ImageFile", table: "DistributorProducts", newName: "ImagenArchivo");
            migrationBuilder.RenameColumn(name: "GeneratedAt", table: "DistributorSyncs", newName: "Generado");
            migrationBuilder.RenameColumn(name: "ImportedAt", table: "DistributorSyncs", newName: "ImportadoAt");
            migrationBuilder.RenameColumn(name: "Count", table: "DistributorSyncs", newName: "Cantidad");
            migrationBuilder.RenameColumn(name: "StartedAt", table: "StockCounts", newName: "IniciadoAt");
            migrationBuilder.RenameColumn(name: "StartedBy", table: "StockCounts", newName: "IniciadoPor");
            migrationBuilder.RenameColumn(name: "FinishedAt", table: "StockCounts", newName: "FinalizadoAt");
            migrationBuilder.RenameColumn(name: "FinishedBy", table: "StockCounts", newName: "FinalizadoPor");
            migrationBuilder.RenameColumn(name: "StockCountId", table: "StockCountItems", newName: "ArqueoId");
            migrationBuilder.RenameColumn(name: "Code", table: "StockCountItems", newName: "Codigo");
            migrationBuilder.RenameColumn(name: "Category", table: "StockCountItems", newName: "Categoria");
            migrationBuilder.RenameColumn(name: "SystemStock", table: "StockCountItems", newName: "StockSistema");
            migrationBuilder.RenameColumn(name: "PurchasePrice", table: "StockCountItems", newName: "PrecioCompra");
            migrationBuilder.RenameColumn(name: "Verified", table: "StockCountItems", newName: "Verificado");
            migrationBuilder.RenameColumn(name: "Difference", table: "StockCountItems", newName: "Diferencia");
            migrationBuilder.RenameColumn(name: "Note", table: "StockCountItems", newName: "Nota");

            migrationBuilder.RenameTable(name: "Customers", newName: "Clientes");
            migrationBuilder.RenameTable(name: "TodoItems", newName: "Tareas");
            migrationBuilder.RenameTable(name: "Expenses", newName: "Egresos");
            migrationBuilder.RenameTable(name: "DistributorProducts", newName: "DistribuidorProductos");
            migrationBuilder.RenameTable(name: "DistributorSyncs", newName: "DistribuidorSyncs");
            migrationBuilder.RenameTable(name: "StockCounts", newName: "Arqueos");
            migrationBuilder.RenameTable(name: "StockCountItems", newName: "ArqueoItems");

            migrationBuilder.CreateIndex(name: "IX_Products_Codigo", table: "Products", column: "Codigo");
            migrationBuilder.CreateIndex(name: "IX_Sales_ClienteId", table: "Sales", column: "ClienteId");
            migrationBuilder.CreateIndex(name: "IX_Users_Nombre", table: "Users", column: "Nombre", unique: true);
            migrationBuilder.CreateIndex(name: "IX_AuditLogs_Fecha", table: "AuditLogs", column: "Fecha");
            migrationBuilder.CreateIndex(name: "IX_Tareas_ClienteId", table: "Tareas", column: "ClienteId");
            migrationBuilder.CreateIndex(name: "IX_DistribuidorProductos_Codigo", table: "DistribuidorProductos", column: "Codigo");
            migrationBuilder.CreateIndex(name: "IX_ArqueoItems_ArqueoId", table: "ArqueoItems", column: "ArqueoId");
        }
    }
}
