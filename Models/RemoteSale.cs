using System.Text.Json;

namespace panialera.Models;

public static class RemoteEventTypes
{
    public const string Sale = "sale";
    public const string Stock = "stock";
    public const string ProductEdit = "productEdit";
    public const string ProductCreate = "productCreate";
    public const string Customer = "customer";
    public const string CustomerEdit = "customerEdit";

    // Types this version applies. Any other type stays Pendiente for a newer version.
    public static bool IsKnown(string type) =>
        type is Sale or Stock or ProductEdit or ProductCreate or Customer or CustomerEdit;
}

// Records that an event of the phone's log was applied. It lives in the same transaction as
// what the event produced, so an event is never applied twice, even after a power cut.
public class RemoteEventImport
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int ResultId { get; set; }
    public DateTime AppliedAt { get; set; }
}

// Content of a "sale" event, as the phone sends it.
public sealed class RemoteSale
{
    public const int CurrentFormatVersion = 1;

    public required int FormatVersion { get; init; }
    public required DateTime Date { get; init; }
    public int? UserId { get; init; }
    public int? CustomerId { get; init; }
    public required PaymentType PaymentType { get; init; }
    public RemoteSaleDiscount? Discount { get; init; }
    public required List<RemoteSaleItem> Items { get; init; }
    public required decimal Total { get; init; }

    public decimal Subtotal => Items.Sum(i => i.SalePrice * i.Quantity);
}

public sealed class RemoteSaleDiscount
{
    public required decimal Value { get; init; }
    public required bool IsPercentage { get; init; }
    public required decimal Amount { get; init; }
    public string? Reason { get; init; }
}

// The cost is not part of the event: it stays off the phone and is filled in when applied.
public sealed class RemoteSaleItem
{
    public required int ProductId { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required int Quantity { get; init; }
    public required decimal SalePrice { get; init; }
}

public record RemoteSaleResult(int SaleId, bool AlreadyApplied, IReadOnlyList<string> Warnings);

// Who made a phone sale, for the audit log, and what to warn about.
public record RemoteSaleOrigin(int? UserId, string UserName, IReadOnlyList<string> Warnings);

// Content of a "stock" event: the phone adjusted a product's stock. The desktop always applies
// Delta to its current stock; SeenStock and NewStock are what the phone showed and what was typed.
public sealed class RemoteStockAdjustment
{
    public const int CurrentFormatVersion = 1;

    public required int FormatVersion { get; init; }
    public required int ProductId { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public int? UserId { get; init; }
    public required int SeenStock { get; init; }
    public required int NewStock { get; init; }
    public required int Delta { get; init; }
}

// WaitingForStockCount: a count is open, so nothing was applied and the event goes back to Pendiente.
public record RemoteStockResult(int ProductId, bool AlreadyApplied, bool WaitingForStockCount, IReadOnlyList<string> Warnings);

// Content of a "productEdit" event: only the fields the user changed, each with the value the
// phone showed and the new one. Applied only if every seen value still matches (the desktop wins).
public sealed class RemoteProductEdit
{
    public const int CurrentFormatVersion = 1;

    public required int FormatVersion { get; init; }
    public required int ProductId { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public int? UserId { get; init; }
    public required Dictionary<string, RemoteFieldChange> Changes { get; init; }
}

public sealed class RemoteFieldChange
{
    public JsonElement Seen { get; init; }
    public JsonElement New { get; init; }
}

// Content of a "productCreate" event. A code that already exists is rejected (no duplicates).
public sealed class RemoteProductCreate
{
    public const int CurrentFormatVersion = 1;

    public required int FormatVersion { get; init; }
    public string? Code { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public string Description { get; init; } = string.Empty;
    public required decimal PurchasePrice { get; init; }
    public required decimal SalePrice { get; init; }
    public int Stock { get; init; }
    public int MinStock { get; init; }
    public int? UserId { get; init; }
    public bool FromDistributor { get; init; }
}

public record RemoteProductResult(int ProductId, bool AlreadyApplied);

// Content of a "customer" event: a new customer, typed on the phone or picked from its contacts.
public sealed class RemoteCustomerCreate
{
    public const int CurrentFormatVersion = 1;

    public required int FormatVersion { get; init; }
    public required string Name { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? Extras { get; init; }
    public string? Optional1 { get; init; }
    public string? Optional2 { get; init; }
    public string? Optional3 { get; init; }
    public string? Optional4 { get; init; }
    public int? UserId { get; init; }
    public bool FromContacts { get; init; }
}

// Content of a "customerEdit" event: only the changed fields, like a product edit.
public sealed class RemoteCustomerEdit
{
    public const int CurrentFormatVersion = 1;

    public required int FormatVersion { get; init; }
    public required int CustomerId { get; init; }
    public required string Name { get; init; }
    public int? UserId { get; init; }
    public required Dictionary<string, RemoteFieldChange> Changes { get; init; }
}

public record RemoteCustomerResult(int CustomerId, bool AlreadyApplied);
