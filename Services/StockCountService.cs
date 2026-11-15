using Microsoft.EntityFrameworkCore;
using panialera.Auth;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

// Totals of a stock count (for the header, the history and the PDF).
public record StockCountSummary(
    int Items, int Verified, int WithDifference,
    int MissingUnits, int ExtraUnits,
    decimal MissingValue, decimal ExtraValue)
{
    public static StockCountSummary Of(StockCount count)
    {
        var missing = count.Items.Where(i => i.Difference < 0).ToList();
        var extra = count.Items.Where(i => i.Difference > 0).ToList();
        return new StockCountSummary(
            count.Items.Count,
            count.Items.Count(i => i.Verified),
            count.Items.Count(i => i.Difference != 0),
            -missing.Sum(i => i.Difference),
            extra.Sum(i => i.Difference),
            -missing.Sum(i => i.Difference * i.PurchasePrice),
            extra.Sum(i => i.Difference * i.PurchasePrice));
    }
}

// Stock count: physical count of every product. It is saved as it is filled in and,
// when finished, the differences are applied to the stock (with audit entries).
public class StockCountService(IDbContextFactory<PanialContext> factory, AuditService audit,
    AppAuthenticationStateProvider auth)
{
    public async Task<StockCount?> GetInProgressAsync()
    {
        await using var db = factory.CreateDbContext();
        var count = await db.StockCounts.AsNoTracking()
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.FinishedAt == null);
        Sort(count);
        return count;
    }

    public async Task<List<StockCount>> GetFinishedAsync()
    {
        await using var db = factory.CreateDbContext();
        var counts = await db.StockCounts.AsNoTracking()
            .Include(a => a.Items)
            .Where(a => a.FinishedAt != null)
            .OrderByDescending(a => a.FinishedAt)
            .ToListAsync();
        counts.ForEach(Sort);
        return counts;
    }

    // Starts a count with a snapshot of the current stock of every product.
    public async Task<(bool ok, string? error)> StartAsync()
    {
        await using var db = factory.CreateDbContext();
        if (await db.StockCounts.AnyAsync(a => a.FinishedAt == null))
            return (false, "Ya hay un arqueo en curso. Finalizalo o descartalo antes de iniciar otro.");

        var products = await db.Products.AsNoTracking().ToListAsync();
        if (products.Count == 0)
            return (false, "No hay productos para arquear.");

        var count = new StockCount
        {
            StartedAt = DateTime.Now,
            StartedBy = auth.CurrentUserName,
            Items = products.Select(p => new StockCountItem
            {
                ProductId = p.Id,
                Code = p.Code,
                ProductName = p.Name,
                ProductDescription = p.Description,
                Category = p.Category,
                SystemStock = p.Stock,
                PurchasePrice = p.PurchasePrice
            }).ToList()
        };
        db.StockCounts.Add(count);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.StockCounts, "Iniciar",
            $"Se inició el arqueo #{count.Id} ({count.Items.Count} productos)",
            null, AuditEntity.StockCount, count.Id);
        return (true, null);
    }

    // Saves one row (check, difference and note). Called on every change.
    public async Task SaveItemAsync(StockCountItem item)
    {
        await using var db = factory.CreateDbContext();
        var current = await db.StockCountItems.Include(i => i.StockCount).FirstOrDefaultAsync(i => i.Id == item.Id);
        if (current?.StockCount == null || current.StockCount.FinishedAt != null)
            return;

        current.Verified = item.Verified;
        current.Difference = item.Difference;
        current.Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim();
        await db.SaveChangesAsync();
    }

    // Closes the count and applies the differences to the stock. The difference is added to
    // the CURRENT stock (it does not overwrite it with the counted value), so a sale made
    // during the count is not lost. Stock never goes below zero.
    public async Task<(bool ok, string? error)> FinishAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var count = await db.StockCounts.Include(a => a.Items).FirstOrDefaultAsync(a => a.Id == id);
        if (count == null || count.FinishedAt != null)
            return (false, "El arqueo no existe o ya fue finalizado.");

        var moves = new List<(StockCountItem item, int before, int after)>();
        var withoutProduct = new List<StockCountItem>();
        foreach (var item in count.Items.Where(i => i.Difference != 0))
        {
            var product = await db.Products.FindAsync(item.ProductId);
            if (product == null)
            {
                withoutProduct.Add(item);
                continue;
            }
            var before = product.Stock;
            product.Stock = Math.Max(0, product.Stock + item.Difference);
            moves.Add((item, before, product.Stock));
        }

        count.FinishedAt = DateTime.Now;
        count.FinishedBy = auth.CurrentUserName;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var s = StockCountSummary.Of(count);
        var details = new List<string>
        {
            $"Productos: {s.Items} · Verificados: {s.Verified} · Con diferencia: {s.WithDifference}",
            $"Faltantes: {s.MissingUnits} u (${s.MissingValue:F2} a costo)",
            $"Sobrantes: {s.ExtraUnits} u (${s.ExtraValue:F2} a costo)"
        };
        foreach (var i in count.Items.Where(i => i.Difference != 0))
            details.Add($"{Signed(i.Difference)} {Label(i)}{(i.Note == null ? "" : $" — {i.Note}")}");
        foreach (var i in withoutProduct)
            details.Add($"Sin ajustar (el producto ya no existe): {Label(i)}");

        await audit.LogAsync(AuditCategory.StockCounts, "Finalizar",
            $"Se finalizó el arqueo #{count.Id}: faltan {s.MissingUnits} u, sobran {s.ExtraUnits} u",
            string.Join("\n", details), AuditEntity.StockCount, count.Id);

        foreach (var (item, before, after) in moves)
        {
            await audit.LogAsync(AuditCategory.Stock, "Ajuste por arqueo",
                $"Ajuste por arqueo #{count.Id} de {Label(item)}: {Signed(item.Difference)}",
                $"{before} → {after}{(item.Note == null ? "" : $"\nNota: {item.Note}")}",
                AuditEntity.Product, item.ProductId);
        }
        return (true, null);
    }

    // Discards a count in progress (the stock is not touched).
    public async Task DiscardAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        var count = await db.StockCounts.FirstOrDefaultAsync(a => a.Id == id && a.FinishedAt == null);
        if (count == null)
            return;
        db.StockCounts.Remove(count);
        await db.SaveChangesAsync();
        await audit.LogAsync(AuditCategory.StockCounts, "Descartar",
            $"Se descartó el arqueo #{id} sin aplicar cambios al stock", null, AuditEntity.StockCount, id);
    }

    // Same order on screen and in the PDF: by category and name (to walk the shop floor).
    private static void Sort(StockCount? count)
    {
        if (count == null)
            return;
        count.Items = count.Items
            .OrderBy(i => string.IsNullOrEmpty(i.Category) ? 1 : 0)
            .ThenBy(i => i.Category)
            .ThenBy(i => i.ProductName)
            .ThenBy(i => i.ProductDescription)
            .ToList();
    }

    private static string Signed(int d) => d > 0 ? $"+{d}" : d.ToString();

    private static string Label(StockCountItem i) =>
        string.IsNullOrWhiteSpace(i.ProductDescription) ? i.ProductName : $"{i.ProductName} - {i.ProductDescription}";
}
