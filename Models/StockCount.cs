namespace panialera.Models;

// Physical stock count ("arqueo"). It starts with a snapshot of the stock of every product
// and is filled in as people count: each item is marked as verified and, when it does not
// match, the difference (+ extra / - missing) is entered with a note. It is saved as it is
// filled in (it can be resumed later) and finishing it applies the differences to the stock.
// Only one count can be in progress at a time.
public class StockCount
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public string? StartedBy { get; set; }

    // Null while in progress.
    public DateTime? FinishedAt { get; set; }
    public string? FinishedBy { get; set; }

    public List<StockCountItem> Items { get; set; } = new();

    public bool IsInProgress => FinishedAt == null;
}

// A row of the count. It keeps a snapshot of the product taken at the start, like sales do,
// so the count stays readable even if the product is later edited or deleted.
public class StockCountItem
{
    public int Id { get; set; }
    public int StockCountId { get; set; }
    public StockCount? StockCount { get; set; }

    // "Soft" reference to the product (no FK), like SaleItem.
    public int ProductId { get; set; }
    public string? Code { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public string? Category { get; set; }

    // Stock according to the system when the count started, and the purchase price at that
    // time (to value missing and extra units).
    public int SystemStock { get; set; }
    public decimal PurchasePrice { get; set; }

    public bool Verified { get; set; }

    // Counted minus system: +3 = 3 extra, -3 = 3 missing.
    public int Difference { get; set; }
    public string? Note { get; set; }

    public int Counted => SystemStock + Difference;
}
