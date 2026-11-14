using Microsoft.EntityFrameworkCore;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

public class ProductService(IDbContextFactory<PanialContext> factory, AuditService audit)
{
    private readonly IDbContextFactory<PanialContext> _factory = factory;

    public async Task<List<Product>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Products
            .Include(p => p.LastUpdatedByUser)
            .OrderBy(p => p.Id)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Products.FindAsync(id);
    }

    public async Task<Product> AddAsync(Product product, int? userId = null)
    {
        await using var db = _factory.CreateDbContext();
        product.LastUpdatedAt = DateTime.Now;
        product.LastUpdatedByUserId = userId;
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // The initial price goes into the price history.
        db.ProductPriceHistories.Add(new ProductPriceHistory
        {
            ProductId = product.Id,
            RecordedAt = DateTime.Now,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice
        });
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.Products, "Crear",
            $"Se creó el producto {Label(product)}", CreateDetails(product),
            AuditEntity.Product, product.Id);

        if (product.Stock != 0)
        {
            await audit.LogAsync(AuditCategory.Stock, "Stock inicial",
                $"Stock inicial de {Label(product)}: {product.Stock}",
                $"0 → {product.Stock}", AuditEntity.Product, product.Id);
        }

        return product;
    }

    private static string CreateDetails(Product product) =>
        (string.IsNullOrWhiteSpace(product.Code) ? "" : $"Código: {product.Code}\n") +
        (string.IsNullOrWhiteSpace(product.Category) ? "" : $"Categoría: {product.Category}\n") +
        $"Precio compra: ${product.PurchasePrice:F2}\nPrecio venta: ${product.SalePrice:F2}\n" +
        $"Stock inicial: {product.Stock}\nStock mínimo: {product.MinStock}" +
        (product.ImageFile == null ? "" : "\nImagen: subida");

    // loadedStock is the stock the edit started from. When given, only the change made in the
    // edit is applied to the current stock, so a sale registered meanwhile is not overwritten.
    // loaded is the whole product as the form loaded it. When given, only the fields changed in
    // the form are written: a phone edit applied meanwhile to another field is kept, and on a
    // field both changed, the desktop wins.
    public async Task<Product> UpdateAsync(Product product, int? userId = null, int? loadedStock = null,
        Product? loaded = null)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Load the previous state to compare and log the changes.
        var original = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);

        if (original != null && loaded != null)
        {
            KeepUnchanged(product, loaded, original);
            loadedStock = loaded.Stock;
        }
        if (original != null && loadedStock.HasValue)
            product.Stock = original.Stock + product.Stock - loadedStock.Value;

        var now = DateTime.Now;
        product.LastUpdatedAt = now;
        product.LastUpdatedByUserId = userId;
        // The product from the list comes with its last editor loaded. Without this, Update also
        // saves that user with their stale data (password hash included) and the editor does not change.
        product.LastUpdatedByUser = null;
        db.Products.Update(product);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (original != null)
        {
            // Changes to the product data (stock excluded).
            var changes = DescribeChanges(original, product);

            // Manual image: if it changed or was removed, the old file is deleted.
            if (original.ImageFile != product.ImageFile)
            {
                changes.Add(product.ImageFile == null ? "Imagen: quitada"
                    : original.ImageFile == null ? "Imagen: subida" : "Imagen: reemplazada");
                DeleteImageFile(original.ImageFile);
            }

            if (changes.Count > 0)
            {
                await audit.LogAsync(AuditCategory.Products, "Editar",
                    $"Se editó el producto {Label(product)}",
                    string.Join("\n", changes), AuditEntity.Product, product.Id);
            }

            // A stock change is logged as a separate Stock movement.
            if (original.Stock != product.Stock)
            {
                var diff = product.Stock - original.Stock;
                await audit.LogAsync(AuditCategory.Stock, "Ajuste manual",
                    $"Ajuste de stock de {Label(product)}: {(diff > 0 ? "+" : "")}{diff}",
                    $"{original.Stock} → {product.Stock}", AuditEntity.Product, product.Id);
            }

            // If a price changed, add an entry to the price history.
            if (original.PurchasePrice != product.PurchasePrice || original.SalePrice != product.SalePrice)
            {
                db.ProductPriceHistories.Add(new ProductPriceHistory
                {
                    ProductId = product.Id,
                    RecordedAt = now,
                    PurchasePrice = product.PurchasePrice,
                    SalePrice = product.SalePrice
                });
                await db.SaveChangesAsync();
            }
        }

        return product;
    }

    // Fields the form did not change take the current database value.
    private static void KeepUnchanged(Product edited, Product loaded, Product current)
    {
        if (edited.Code == loaded.Code) edited.Code = current.Code;
        if (edited.Name == loaded.Name) edited.Name = current.Name;
        if (edited.Category == loaded.Category) edited.Category = current.Category;
        if (edited.Description == loaded.Description) edited.Description = current.Description;
        if (edited.PurchasePrice == loaded.PurchasePrice) edited.PurchasePrice = current.PurchasePrice;
        if (edited.SalePrice == loaded.SalePrice) edited.SalePrice = current.SalePrice;
        if (edited.MinStock == loaded.MinStock) edited.MinStock = current.MinStock;
        if (edited.ImageFile == loaded.ImageFile) edited.ImageFile = current.ImageFile;
    }

    // "Field: before → after" for each data field that changed (stock excluded).
    private static List<string> DescribeChanges(Product before, Product after)
    {
        var changes = new List<string>();
        foreach (var field in RemoteProductFields.All)
        {
            var a = RemoteProductFields.Show(before, field);
            var b = RemoteProductFields.Show(after, field);
            if (a != b)
                changes.Add($"{RemoteProductFields.Label(field)}: {a} → {b}");
        }
        return changes;
    }

    // Applies a product edit made on the phone, only if every value the phone saw still matches.
    // Otherwise the desktop wins: nothing changes and the event is rejected with both values.
    public async Task<RemoteProductResult> ApplyRemoteEditAsync(string eventId, RemoteProductEdit remote, string createdBy)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var applied = await db.RemoteEventImports.FindAsync(eventId);
        if (applied != null)
            return new RemoteProductResult(applied.ResultId, AlreadyApplied: true);

        var product = await db.Products.FindAsync(remote.ProductId)
            ?? throw new EventRejectedException($"{remote.Name} ya no existe: no se aplicó el cambio.");

        var conflicts = remote.Changes
            .Where(c => !RemoteProductFields.Matches(product, c.Key, c.Value.Seen))
            .Select(c => $"{RemoteProductFields.Label(c.Key)}: en el celular se veía {RemoteProductFields.Show(c.Key, c.Value.Seen)} " +
                         $"y en el escritorio es {RemoteProductFields.Show(product, c.Key)}")
            .ToList();
        if (conflicts.Count > 0)
            throw new EventRejectedException(
                $"{Label(product)} cambió mientras tanto y no se aplicó. {string.Join(". ", conflicts)}.");

        var userId = await ExistingUserAsync(db, remote.UserId);
        var before = Copy(product);
        foreach (var (field, change) in remote.Changes)
            RemoteProductFields.Set(product, field, change.New);
        var now = DateTime.Now;
        product.LastUpdatedAt = now;
        product.LastUpdatedByUserId = userId;

        var changes = DescribeChanges(before, product);
        if (changes.Count > 0)
        {
            db.AuditLogs.Add(audit.Entry(AuditCategory.Products, "Editar desde el celular",
                $"Se editó el producto {Label(product)}", string.Join("\n", changes),
                AuditEntity.Product, product.Id, userId, createdBy));
        }
        if (before.PurchasePrice != product.PurchasePrice || before.SalePrice != product.SalePrice)
        {
            db.ProductPriceHistories.Add(new ProductPriceHistory
            {
                ProductId = product.Id,
                RecordedAt = now,
                PurchasePrice = product.PurchasePrice,
                SalePrice = product.SalePrice
            });
        }

        db.RemoteEventImports.Add(new RemoteEventImport
        {
            EventId = eventId,
            EventType = RemoteEventTypes.ProductEdit,
            ResultId = product.Id,
            AppliedAt = now
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new RemoteProductResult(product.Id, AlreadyApplied: false);
    }

    // Creates a product made on the phone. A code the shop already has is rejected, so the same
    // product is never created twice (DEC-017 of the phone app).
    public async Task<RemoteProductResult> ApplyRemoteCreateAsync(string eventId, RemoteProductCreate remote, string createdBy)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var applied = await db.RemoteEventImports.FindAsync(eventId);
        if (applied != null)
            return new RemoteProductResult(applied.ResultId, AlreadyApplied: true);

        var code = string.IsNullOrWhiteSpace(remote.Code) ? null : remote.Code.Trim();
        if (code != null)
        {
            // Compared in memory: SQLite only ignores case for ASCII (not for Ñ or accents).
            var withCode = await db.Products.AsNoTracking().Where(p => p.Code != null).ToListAsync();
            var existing = withCode.FirstOrDefault(p => p.Code!.Trim().Equals(code, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                throw new EventRejectedException($"Ya existe {Label(existing)} con el código {code}: no se creó.");
        }

        var userId = await ExistingUserAsync(db, remote.UserId);
        var now = DateTime.Now;
        var product = new Product
        {
            Code = code,
            Name = remote.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(remote.Category) ? null : remote.Category.Trim(),
            Description = remote.Description ?? string.Empty,
            PurchasePrice = remote.PurchasePrice,
            SalePrice = remote.SalePrice,
            Stock = remote.Stock,
            MinStock = remote.MinStock,
            LastUpdatedAt = now,
            LastUpdatedByUserId = userId
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.ProductPriceHistories.Add(new ProductPriceHistory
        {
            ProductId = product.Id,
            RecordedAt = now,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice
        });
        db.AuditLogs.Add(audit.Entry(AuditCategory.Products, "Crear desde el celular",
            $"Se creó el producto {Label(product)}",
            CreateDetails(product) + (remote.FromDistributor ? "\nDesde el catálogo del distribuidor" : ""),
            AuditEntity.Product, product.Id, userId, createdBy));
        if (product.Stock != 0)
        {
            db.AuditLogs.Add(audit.Entry(AuditCategory.Stock, "Stock inicial",
                $"Stock inicial de {Label(product)}: {product.Stock}",
                $"0 → {product.Stock}", AuditEntity.Product, product.Id, userId, createdBy));
        }

        db.RemoteEventImports.Add(new RemoteEventImport
        {
            EventId = eventId,
            EventType = RemoteEventTypes.ProductCreate,
            ResultId = product.Id,
            AppliedAt = now
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new RemoteProductResult(product.Id, AlreadyApplied: false);
    }

    private static async Task<int?> ExistingUserAsync(PanialContext db, int? userId) =>
        userId.HasValue && await db.Users.AnyAsync(u => u.Id == userId.Value) ? userId : null;

    private static Product Copy(Product p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        Name = p.Name,
        Category = p.Category,
        Description = p.Description,
        Stock = p.Stock,
        PurchasePrice = p.PurchasePrice,
        SalePrice = p.SalePrice,
        MinStock = p.MinStock,
        ImageFile = p.ImageFile
    };

    // A stock count is open: stock adjustments from the phone wait until it ends (its
    // differences would count the same goods twice).
    public async Task<bool> HasOpenStockCountAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.StockCounts.AnyAsync(c => c.FinishedAt == null);
    }

    // Applies a stock adjustment made on the phone. The delta goes on top of the current stock,
    // so a sale registered meanwhile is not overwritten. The event id is stored in the same
    // transaction, so the same event never adjusts twice.
    public async Task<RemoteStockResult> ApplyRemoteStockAsync(string eventId, RemoteStockAdjustment remote, string createdBy)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var applied = await db.RemoteEventImports.FindAsync(eventId);
        if (applied != null)
            return new RemoteStockResult(applied.ResultId, AlreadyApplied: true, WaitingForStockCount: false, []);

        if (await db.StockCounts.AnyAsync(c => c.FinishedAt == null))
            return new RemoteStockResult(remote.ProductId, AlreadyApplied: false, WaitingForStockCount: true, []);

        var product = await db.Products.FindAsync(remote.ProductId)
            ?? throw new EventRejectedException($"{remote.Name} ya no existe: no se ajustó el stock.");

        var userId = await ExistingUserAsync(db, remote.UserId);

        var warnings = new List<string>();
        var before = product.Stock;
        product.Stock += remote.Delta;
        product.LastUpdatedAt = DateTime.Now;
        product.LastUpdatedByUserId = userId;
        if (product.Stock < 0)
            warnings.Add($"{Label(product)} quedó con stock negativo ({product.Stock}): revisalo.");

        var details = $"{before} → {product.Stock}";
        if (before != remote.SeenStock)
            details += $"\nEn el celular se veía {remote.SeenStock} y se cargó {remote.NewStock}.";
        db.AuditLogs.Add(audit.Entry(AuditCategory.Stock, "Ajuste desde el celular",
            $"Ajuste de stock de {Label(product)}: {(remote.Delta > 0 ? "+" : "")}{remote.Delta}",
            details, AuditEntity.Product, product.Id, userId, createdBy));

        db.RemoteEventImports.Add(new RemoteEventImport
        {
            EventId = eventId,
            EventType = RemoteEventTypes.Stock,
            ResultId = product.Id,
            AppliedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new RemoteStockResult(product.Id, AlreadyApplied: false, WaitingForStockCount: false, warnings);
    }

    public async Task<Product?> GetByIdWithUserAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Products
            .Include(p => p.LastUpdatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<ProductPriceHistory>> GetPriceHistoryAsync(int productId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.ProductPriceHistories
            .Where(ph => ph.ProductId == productId)
            .OrderByDescending(ph => ph.RecordedAt)
            .ToListAsync();
    }

    // Product history (edits and stock movements), from the audit log.
    public Task<List<AuditLog>> GetChangesAsync(int productId) =>
        audit.GetForEntityAsync(AuditEntity.Product, productId);

    // The shop's products with this code (the code is not unique).
    public async Task<List<Product>> GetByCodeAsync(string code)
    {
        var c = code.Trim();
        await using var db = _factory.CreateDbContext();
        // Compared in memory: SQLite only ignores case for ASCII (not for Ñ or accents).
        var withCode = await db.Products.AsNoTracking().Where(p => p.Code != null).ToListAsync();
        return withCode.Where(p => p.Code!.Trim().Equals(c, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var product = await db.Products.FindAsync(id);
        if (product != null)
        {
            db.Products.Remove(product);
            await db.SaveChangesAsync();
            DeleteImageFile(product.ImageFile);

            await audit.LogAsync(AuditCategory.Products, "Eliminar",
                $"Se eliminó el producto {Label(product)}",
                $"Stock al eliminar: {product.Stock}", AuditEntity.Product, id);
        }
    }

    // Categories already used by the shop's products (suggested in the form).
    public async Task<List<string>> GetCategoriesAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Products
            .Where(p => p.Category != null && p.Category != "")
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    // Saves an image uploaded by hand and returns the file name for Product.ImageFile.
    // The name is unique, so the WebView never shows an old cached version.
    public async Task<string> SaveImageAsync(byte[] data, string extension)
    {
        var file = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await File.WriteAllBytesAsync(Path.Combine(AppImages.ProductsDir, file), data);
        return file;
    }

    private static void DeleteImageFile(string? file)
    {
        if (string.IsNullOrEmpty(file))
            return;
        try
        {
            var path = Path.Combine(AppImages.ProductsDir, Path.GetFileName(file));
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            // Not serious: an orphan file stays in the folder.
            Logger.Error($"Could not delete image {file}", ex);
        }
    }

    private static string Label(Product p) =>
        string.IsNullOrWhiteSpace(p.Description) ? p.Name : $"{p.Name} - {p.Description}";
}
