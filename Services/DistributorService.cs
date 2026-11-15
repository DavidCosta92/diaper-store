using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using panialera.Auth;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

public record DistributorOptions(string ScraperPath);

// Distributor's catalog: runs the scraper (Scrapper.exe), imports the catalogo.json it
// writes and resolves product images by code for the shop's own products.
// It is a singleton: a sync keeps running when the user changes screens, and coming back
// to the screen shows its progress.
public partial class DistributorService(
    IDbContextFactory<PanialContext> factory,
    AuditService audit,
    AppAuthenticationStateProvider authState,
    DistributorOptions options)
{
    // File written by the scraper. Name and JSON keys are a contract with Scrapper/JsonExporter.
    private const string CatalogFileName = "catalogo.json";
    private const int MaxLogLines = 300;
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(20);

    private readonly List<string> _log = new();
    private ImageCache? _images;

    public bool IsRunning { get; private set; }

    // Progress shown to the user: readable stage and percentage (0-100).
    public string Stage { get; private set; } = string.Empty;
    public int Progress { get; private set; }

    // Result of the last run (shown until the user dismisses it).
    public bool? LastOk { get; private set; }
    public string? LastMessage { get; private set; }

    // Raised when the state or the progress changes (from any thread).
    public event Action? Changed;

    public string ScraperPath => options.ScraperPath;
    public bool ScraperAvailable => File.Exists(options.ScraperPath);
    public bool HasCatalogFile => File.Exists(Path.Combine(AppImages.DistributorDir, CatalogFileName));

    // Raw scraper output (technical detail; it also goes to the file log).
    public IReadOnlyList<string> GetLog()
    {
        lock (_log) return _log.ToList();
    }

    public void DismissResult()
    {
        LastOk = null;
        LastMessage = null;
        Notify();
    }

    public async Task<List<DistributorProduct>> GetAllAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.DistributorProducts.AsNoTracking().OrderBy(d => d.Code).ToListAsync();
    }

    public async Task<DistributorProduct?> GetByIdAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        return await db.DistributorProducts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
    }

    // Catalog codes, to flag which of the shop's products the distributor has.
    public async Task<HashSet<string>> GetCodesAsync()
    {
        await using var db = factory.CreateDbContext();
        var codes = await db.DistributorProducts
            .Where(d => d.Code != "")
            .Select(d => d.Code)
            .Distinct()
            .ToListAsync();
        return codes.Select(c => c.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<DistributorSync?> GetLastSyncAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.DistributorSyncs.AsNoTracking().OrderByDescending(s => s.Id).FirstOrDefaultAsync();
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.DistributorProducts
            .Where(d => d.Category != null && d.Category != "")
            .Select(d => d.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    // Image URL of a catalog product (null when it has none).
    public async Task<Func<DistributorProduct, string?>> GetCatalogUrlResolverAsync()
    {
        var cache = await GetImagesAsync();
        return d => string.IsNullOrEmpty(d.ImageFile) ? null : AppImages.CatalogUrl(d.ImageFile, cache.Version);
    }

    // Returns a function that gives the image URL of a shop product: its uploaded photo if
    // it has one; otherwise the catalog image with the same code; otherwise null.
    public async Task<Func<Product, string?>> GetProductUrlResolverAsync()
    {
        var cache = await GetImagesAsync();
        return p =>
        {
            if (!string.IsNullOrEmpty(p.ImageFile))
                return AppImages.ProductUrl(p.ImageFile);
            if (!string.IsNullOrWhiteSpace(p.Code) && cache.ByCode.TryGetValue(p.Code.Trim(), out var file))
                return AppImages.CatalogUrl(file, cache.Version);
            return null;
        };
    }

    // Runs the scraper and, if it succeeds, imports the catalog. Returns false on failure.
    public Task<bool> SyncAsync() => RunAsync(async () =>
    {
        if (!ScraperAvailable)
            throw new FileNotFoundException(
                $"No se encontró el scraper en {options.ScraperPath}. Revisá ScraperPath en appsettings.json.");

        SetStage("Conectando con el distribuidor...", 1);
        await RunScraperAsync();
        await ImportCoreAsync();
    }, "Falló la sincronización con el distribuidor");

    // Imports the last downloaded catalogo.json again, without running the scraper.
    public Task<bool> ReimportAsync() =>
        RunAsync(ImportCoreAsync, "Falló la importación del catálogo del distribuidor");

    private async Task<bool> RunAsync(Func<Task> work, string errorContext)
    {
        if (IsRunning)
            return false;

        IsRunning = true;
        LastOk = null;
        LastMessage = null;
        lock (_log) _log.Clear();
        _range = (0, 0);
        SetStage("Preparando...", 0);

        try
        {
            await work();
            LastOk = true;
            return true;
        }
        catch (Exception ex)
        {
            AddLog($"ERROR: {ex.Message}");
            LastOk = false;
            LastMessage = $"{errorContext}: {ex.Message}";
            ErrorHub.Report(ex, errorContext);
            return false;
        }
        finally
        {
            IsRunning = false;
            Notify();
        }
    }

    private async Task RunScraperAsync()
    {
        var psi = new ProcessStartInfo(options.ScraperPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(options.ScraperPath) ?? AppContext.BaseDirectory
        };
        // Leaves catalogo.json in DistributorDir and the images in DistributorDir\imagenes.
        psi.ArgumentList.Add(AppImages.DistributorDir);
        psi.ArgumentList.Add("--no-csv");

        using var proc = new Process { StartInfo = psi };
        string? lastError = null;
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            AddLog(e.Data);
            UpdateProgress(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            lastError = e.Data;
            AddLog("⚠ " + e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(MaxDuration);
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
            throw new TimeoutException($"El scraper tardó más de {MaxDuration.TotalMinutes:F0} minutos y se canceló.");
        }

        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"El scraper terminó con error (código {proc.ExitCode}). {lastError}".Trim());
    }

    private async Task ImportCoreAsync()
    {
        var path = Path.Combine(AppImages.DistributorDir, CatalogFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"No se encontró el archivo del catálogo: {path}");

        SetStage("Guardando el catálogo...", 96);
        CatalogFile? catalog;
        await using (var stream = File.OpenRead(path))
        {
            catalog = await JsonSerializer.DeserializeAsync<CatalogFile>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        if (catalog?.Products == null)
            throw new InvalidDataException("El archivo del catálogo no tiene productos.");

        var items = catalog.Products
            .Where(p => !string.IsNullOrWhiteSpace(p.Code) || !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new DistributorProduct
            {
                ItemId = p.ItemId ?? string.Empty,
                Code = p.Code?.Trim() ?? string.Empty,
                Name = p.Name?.Trim() ?? string.Empty,
                Category = EmptyToNull(p.Category),
                Description = EmptyToNull(p.Description),
                WebPrice = p.WebPrice,
                InStock = p.InStock,
                Alert = EmptyToNull(p.Alert),
                ImageFile = string.IsNullOrWhiteSpace(p.ImageFile) ? null : Path.GetFileName(p.ImageFile)
            })
            .ToList();

        var sync = new DistributorSync
        {
            GeneratedAt = catalog.GeneratedAt == default ? File.GetLastWriteTime(path) : catalog.GeneratedAt,
            ImportedAt = DateTime.Now,
            Count = items.Count,
            UserName = authState.CurrentUserName
        };

        // The previous catalog is deleted and the new one loaded, all or nothing.
        await using (var db = factory.CreateDbContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.DistributorProducts.ExecuteDeleteAsync();
            db.DistributorProducts.AddRange(items);
            db.DistributorSyncs.Add(sync);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        _images = null; // rebuilt with the new version
        var withImage = items.Count(i => i.ImageFile != null);
        AddLog($"Done: {items.Count} products imported ({withImage} with image).");
        LastMessage = $"Catálogo actualizado: {items.Count} productos ({withImage} con imagen).";
        SetStage("Listo", 100);

        await audit.LogAsync(AuditCategory.Distributor, "Sincronizar",
            $"Se importó el catálogo del distribuidor: {items.Count} productos",
            $"Generado: {sync.GeneratedAt:dd/MM/yyyy HH:mm}\nCon imagen: {withImage}\nCon stock: {items.Count(i => i.InStock)}");
    }

    private async Task<ImageCache> GetImagesAsync()
    {
        var cache = _images;
        if (cache != null)
            return cache;

        await using var db = factory.CreateDbContext();
        var rows = await db.DistributorProducts
            .Where(d => d.ImageFile != null && d.Code != "")
            .Select(d => new { d.Code, d.ImageFile })
            .ToListAsync();
        var last = await db.DistributorSyncs.OrderByDescending(s => s.Id).Select(s => s.ImportedAt).FirstOrDefaultAsync();

        // If a code repeats in the catalog, the first one wins.
        var byCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
            byCode.TryAdd(r.Code, r.ImageFile!);

        cache = new ImageCache(byCode, last.Ticks);
        _images = cache;
        return cache;
    }

    // Scraper stages and the slice of the progress bar each one takes (from, to).
    // The keys match the lines the scraper prints at the start of each stage.
    private static readonly Dictionary<string, (string Stage, int From, int To)> ScraperStages = new()
    {
        ["Fetching items"] = ("Consultando el listado de productos...", 2, 10),
        ["Fetching details"] = ("Descargando el detalle de los productos...", 10, 60),
        ["Downloading images"] = ("Descargando las imágenes...", 60, 95),
    };

    private (int From, int To) _range = (0, 0);

    // The scraper prints "Fetching details..." when a stage starts and "  n/total" as it
    // progresses; the overall percentage comes from those lines.
    private void UpdateProgress(string line)
    {
        foreach (var (prefix, stage) in ScraperStages)
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _range = (stage.From, stage.To);
                SetStage(stage.Stage, stage.From);
                return;
            }
        }

        var m = ProgressRegex().Match(line);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && int.TryParse(m.Groups[2].Value, out var total) && total > 0)
        {
            var pct = _range.From + (_range.To - _range.From) * Math.Min(n, total) / total;
            SetStage(Stage, pct);
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*(\d+)/(\d+)\s*$")]
    private static partial System.Text.RegularExpressions.Regex ProgressRegex();

    private void SetStage(string stage, int progress)
    {
        Stage = stage;
        Progress = Math.Clamp(progress, 0, 100);
        Notify();
    }

    private void AddLog(string line)
    {
        Logger.Info($"[Distributor] {line}");
        lock (_log)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines)
                _log.RemoveRange(0, _log.Count - MaxLogLines);
        }
        Notify();
    }

    private void Notify() => Changed?.Invoke();

    private static string? EmptyToNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private sealed record ImageCache(Dictionary<string, string> ByCode, long Version);

    // Shape of catalogo.json (written by the scraper's JsonExporter; keys kept in Spanish).
    private sealed class CatalogFile
    {
        [JsonPropertyName("generado")] public DateTime GeneratedAt { get; set; }
        [JsonPropertyName("productos")] public List<CatalogFileItem>? Products { get; set; }
    }

    private sealed class CatalogFileItem
    {
        [JsonPropertyName("itemId")] public string? ItemId { get; set; }
        [JsonPropertyName("codigo")] public string? Code { get; set; }
        [JsonPropertyName("nombre")] public string? Name { get; set; }
        [JsonPropertyName("categoria")] public string? Category { get; set; }
        [JsonPropertyName("descripcion")] public string? Description { get; set; }
        [JsonPropertyName("precioWeb")] public decimal? WebPrice { get; set; }
        [JsonPropertyName("conStock")] public bool InStock { get; set; }
        [JsonPropertyName("imagenArchivo")] public string? ImageFile { get; set; }
        [JsonPropertyName("alerta")] public string? Alert { get; set; }
    }
}
