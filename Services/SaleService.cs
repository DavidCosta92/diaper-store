using Microsoft.EntityFrameworkCore;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

public class SaleService(IDbContextFactory<PanialContext> factory, ProductService productService, AuditService audit)
{
    private readonly IDbContextFactory<PanialContext> _factory = factory;
    private readonly ProductService _productService = productService;

    public async Task<List<Sale>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        var sales = await db.Sales
            .Include(s => s.User)
            .Include(s => s.Customer)
            .Include(s => s.Items)
            .OrderByDescending(s => s.Id)
            .ToListAsync();

        var fromPhone = (await db.RemoteEventImports
            .Where(r => r.EventType == RemoteEventTypes.Sale)
            .Select(r => r.ResultId)
            .ToListAsync()).ToHashSet();
        foreach (var sale in sales)
            sale.FromPhone = fromPhone.Contains(sale.Id);
        return sales;
    }

    public async Task<Sale?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Sale> CreateSaleAsync(Sale sale)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await SaveSaleAsync(db, sale);
        await transaction.CommitAsync();
        return sale;
    }

    // Registers a sale made on the phone. The event id is stored in the same transaction as
    // the sale, so applying the same event again never duplicates it. A sale already happened,
    // so low stock does not reject it: the stock can go negative and a warning is returned.
    public async Task<RemoteSaleResult> ApplyRemoteSaleAsync(string eventId, RemoteSale remote, string createdBy)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var applied = await db.RemoteEventImports.FindAsync(eventId);
        if (applied != null)
            return new RemoteSaleResult(applied.ResultId, AlreadyApplied: true, []);

        var warnings = new List<string>();

        var userId = remote.UserId;
        if (userId.HasValue && !await db.Users.AnyAsync(u => u.Id == userId.Value))
        {
            warnings.Add($"El vendedor #{userId} ya no existe: la venta queda sin vendedor.");
            userId = null;
        }

        var customerId = remote.CustomerId;
        if (customerId.HasValue && !await db.Customers.AnyAsync(c => c.Id == customerId.Value))
        {
            warnings.Add($"El cliente #{customerId} ya no existe: la venta queda sin cliente.");
            customerId = null;
        }

        var items = new List<SaleItem>();
        foreach (var item in remote.Items)
        {
            var product = await db.Products.FindAsync(item.ProductId);
            if (product == null)
                warnings.Add($"{item.Name} ya no existe: no se descontó stock y el costo queda en $0.");

            items.Add(new SaleItem
            {
                ProductId = item.ProductId,
                ProductName = item.Name,
                ProductDescription = item.Description,
                Quantity = item.Quantity,
                SalePrice = item.SalePrice,
                PurchasePrice = product?.PurchasePrice ?? 0m,
                PaymentType = remote.PaymentType,
                CreatedAt = remote.Date
            });
        }

        foreach (var sold in remote.Items.GroupBy(i => i.ProductId))
        {
            var product = await db.Products.FindAsync(sold.Key);
            var remaining = product?.Stock - sold.Sum(i => i.Quantity);
            if (remaining < 0)
                warnings.Add($"{sold.First().Name} quedó con stock negativo ({remaining}): revisalo.");
        }

        var sale = new Sale
        {
            Date = remote.Date,
            UserId = userId,
            CustomerId = customerId,
            DiscountValue = remote.Discount?.Value ?? 0m,
            DiscountIsPercentage = remote.Discount?.IsPercentage ?? false,
            DiscountAmount = remote.Discount?.Amount ?? 0m,
            DiscountReason = remote.Discount?.Reason,
            Items = items
        };

        await SaveSaleAsync(db, sale, new RemoteSaleOrigin(userId, createdBy, warnings));

        db.RemoteEventImports.Add(new RemoteEventImport
        {
            EventId = eventId,
            EventType = RemoteEventTypes.Sale,
            ResultId = sale.Id,
            AppliedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new RemoteSaleResult(sale.Id, AlreadyApplied: false, warnings);
    }

    // Saves the sale, its audit entries and the stock movements. The caller owns the
    // transaction: a transaction started by EF Core on SQLite takes the write lock from the
    // beginning, so the stock read here cannot change before it is saved.
    private async Task SaveSaleAsync(PanialContext db, Sale sale, RemoteSaleOrigin? origin = null)
    {
        db.Sales.Add(sale);
        await db.SaveChangesAsync();

        // Audit entry for the sale.
        var subtotal = sale.Items.Sum(i => i.SalePrice * i.Quantity);
        var total = subtotal - sale.DiscountAmount;
        var payment = sale.Items.FirstOrDefault()?.PaymentType == PaymentType.MercadoPago ? "Mercado Pago" : "Efectivo";
        var customer = sale.CustomerId.HasValue
            ? (await db.Customers.FindAsync(sale.CustomerId.Value))?.Name
            : null;

        var details = new List<string>();
        foreach (var i in sale.Items)
            details.Add($"{i.Quantity} x {i.ProductName} - {i.ProductDescription} @ ${i.SalePrice:F2} = ${i.SalePrice * i.Quantity:F2}");
        details.Add($"Subtotal: ${subtotal:F2}");
        if (sale.DiscountAmount > 0)
            details.Add($"Descuento{(string.IsNullOrWhiteSpace(sale.DiscountReason) ? "" : $" ({sale.DiscountReason})")}: -${sale.DiscountAmount:F2}");
        details.Add($"Total: ${total:F2}");
        details.Add($"Pago: {payment}");
        if (customer != null)
            details.Add($"Cliente: {customer}");

        if (origin != null)
        {
            details.Add("Origen: celular");
            details.AddRange(origin.Warnings.Select(w => $"Aviso: {w}"));
        }

        db.AuditLogs.Add(audit.Entry(AuditCategory.Sales, "Registrar",
            $"Se registró la venta #{sale.Id} por ${total:F2}{(origin == null ? "" : " desde el celular")}",
            string.Join("\n", details), AuditEntity.Sale, sale.Id, origin?.UserId, origin?.UserName));

        // Reduce the stock of each item and log the movement.
        foreach (var item in sale.Items)
        {
            var product = await db.Products.FindAsync(item.ProductId);
            if (product == null)
                continue;

            var before = product.Stock;
            product.Stock -= item.Quantity;
            db.AuditLogs.Add(audit.Entry(AuditCategory.Stock, "Salida por venta",
                $"Salida de {item.ProductName} - {item.ProductDescription}: -{item.Quantity} (venta #{sale.Id})",
                $"{before} → {product.Stock}", AuditEntity.Product, item.ProductId, origin?.UserId, origin?.UserName));
        }

        await db.SaveChangesAsync();
    }

    // Voids a sale (it is never deleted) and puts its items back in stock.
    public async Task VoidSaleAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var sale = await db.Sales
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale != null && !sale.Voided)
        {
            sale.Voided = true;

            // The stock is restored directly (not through ProductService.UpdateAsync) so the
            // product's "last change" is not touched.
            var moves = new List<(SaleItem item, int before, int after)>();
            foreach (var item in sale.Items)
            {
                var product = await db.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    var before = product.Stock;
                    product.Stock += item.Quantity;
                    moves.Add((item, before, product.Stock));
                }
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            var total = sale.Items.Sum(i => i.SalePrice * i.Quantity) - sale.DiscountAmount;
            await audit.LogAsync(AuditCategory.Sales, "Anular",
                $"Se anuló la venta #{sale.Id} (${total:F2})",
                string.Join("\n", sale.Items.Select(i => $"{i.Quantity} x {i.ProductName} - {i.ProductDescription}")),
                AuditEntity.Sale, sale.Id);

            foreach (var (item, before, after) in moves)
            {
                await audit.LogAsync(AuditCategory.Stock, "Reingreso por anulación",
                    $"Reingreso de {item.ProductName} - {item.ProductDescription}: +{item.Quantity} (venta #{sale.Id} anulada)",
                    $"{before} → {after}", AuditEntity.Product, item.ProductId);
            }
        }
    }

    public async Task<bool> ValidateStockAsync(int productId, int quantity)
    {
        var product = await _productService.GetByIdAsync(productId);
        return product != null && product.Stock >= quantity;
    }

    public async Task<List<ProfitReportRow>> GetProfitReportAsync(
        DateTime? from = null, DateTime? to = null, int? productId = null, int? userId = null,
        PaymentType? paymentType = null)
    {
        await using var db = _factory.CreateDbContext();

        // Only sales that were not voided.
        var query = db.SaleItems
            .Include(si => si.Sale)
            .Where(si => si.Sale != null && !si.Sale.Voided);

        if (from.HasValue)
        {
            var fromDate = from.Value.Date;
            query = query.Where(si => si.Sale!.Date >= fromDate);
        }

        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            query = query.Where(si => si.Sale!.Date < toExclusive);
        }

        if (productId.HasValue)
        {
            query = query.Where(si => si.ProductId == productId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(si => si.Sale!.UserId == userId.Value);
        }

        if (paymentType.HasValue)
        {
            query = query.Where(si => si.PaymentType == paymentType.Value);
        }

        var items = await query.ToListAsync();

        return items
            .GroupBy(si => si.ProductId)
            .Select(g => new ProfitReportRow
            {
                ProductId = g.Key,
                ProductName = g.First().ProductName,
                ProductDescription = g.First().ProductDescription,
                UnitsSold = g.Sum(si => si.Quantity),
                TotalCost = g.Sum(si => si.PurchasePrice * si.Quantity),
                TotalRevenue = g.Sum(si => si.SalePrice * si.Quantity)
            })
            .OrderByDescending(r => r.TotalProfit)
            .ToList();
    }

    // Discounts given (sales not voided), grouped by reason, for the report.
    public async Task<List<DiscountReportRow>> GetDiscountReportAsync(
        DateTime? from = null, DateTime? to = null, int? userId = null, PaymentType? paymentType = null)
    {
        await using var db = _factory.CreateDbContext();

        var query = db.Sales.Where(s => !s.Voided && s.DiscountAmount > 0);

        if (from.HasValue)
        {
            var fromDate = from.Value.Date;
            query = query.Where(s => s.Date >= fromDate);
        }
        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            query = query.Where(s => s.Date < toExclusive);
        }
        if (userId.HasValue)
        {
            query = query.Where(s => s.UserId == userId.Value);
        }
        if (paymentType.HasValue)
        {
            // The payment type lives on the items; a sale has a single type.
            query = query.Where(s => s.Items.Any(i => i.PaymentType == paymentType.Value));
        }

        var sales = await query
            .Select(s => new { s.DiscountReason, s.DiscountAmount })
            .ToListAsync();

        return sales
            .GroupBy(v => string.IsNullOrWhiteSpace(v.DiscountReason) ? "Sin motivo" : v.DiscountReason!.Trim())
            .Select(g => new DiscountReportRow
            {
                Reason = g.Key,
                Count = g.Count(),
                Total = g.Sum(v => v.DiscountAmount)
            })
            .OrderByDescending(r => r.Total)
            .ToList();
    }
}
