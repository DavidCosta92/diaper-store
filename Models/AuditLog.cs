namespace panialera.Models;

// Main area (category) of an audit log entry.
// Stored as text in the database, so adding or reordering values does not break old rows.
public enum AuditCategory
{
    Products,
    Stock,
    Sales,
    Customers,
    Expenses,
    Todos,
    Users,
    Distributor,
    StockCounts,
    Session,
    System,
    Errors
}

public static class AuditCategoryExtensions
{
    // Name shown in the UI.
    public static string DisplayName(this AuditCategory c) => c switch
    {
        AuditCategory.Products => "Productos",
        AuditCategory.Stock => "Stock",
        AuditCategory.Sales => "Ventas",
        AuditCategory.Customers => "Clientes",
        AuditCategory.Expenses => "Egresos",
        AuditCategory.Todos => "Pendientes",
        AuditCategory.Users => "Usuarios",
        AuditCategory.Distributor => "Distribuidor",
        AuditCategory.StockCounts => "Arqueos",
        AuditCategory.Session => "Sesión",
        AuditCategory.System => "Sistema",
        AuditCategory.Errors => "Errores",
        _ => c.ToString()
    };
}

// Values stored in AuditLog.EntityType, used to show the history of a single record.
public static class AuditEntity
{
    public const string Product = "Product";
    public const string Sale = "Sale";
    public const string Customer = "Customer";
    public const string Expense = "Expense";
    public const string TodoItem = "TodoItem";
    public const string User = "User";
    public const string StockCount = "StockCount";
}

// An audit log entry (traceability of operations).
public class AuditLog
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;

    // User who performed the operation. The name is stored too (snapshot) so the log stays
    // readable after the user is deleted. No FK on purpose.
    public int? UserId { get; set; }
    public string? UserName { get; set; }

    public AuditCategory Category { get; set; }
    public string Action { get; set; } = string.Empty;       // e.g. Crear, Editar, Ajuste manual
    public string Description { get; set; } = string.Empty;  // readable text of the operation
    public string? Details { get; set; }                     // e.g. "field: before → after" lines

    // Affected record (optional), to show the history of a single item. See AuditEntity.
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
}
