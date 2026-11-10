namespace panialera.Models;

public static class RemoteEventTypes
{
    public const string Sale = "sale";
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
