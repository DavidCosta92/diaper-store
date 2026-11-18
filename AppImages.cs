namespace panialera;

// Image folders and URLs. The images live next to the database:
//   <database folder>\imagenes\productos       -> photos uploaded by hand
//   <database folder>\distribuidor             -> the scraper's catalogo.json
//   <database folder>\distribuidor\imagenes    -> photos from the distributor's catalog
// The folder names stay in Spanish because installed copies already have data in them.
// The WebView cannot read local files directly: each image folder is mapped to a virtual
// host (see MainForm) and the <img> tags use those URLs.
public static class AppImages
{
    public const string ProductsHost = "fotos.carinitos";
    public const string CatalogHost = "catalogo.carinitos";

    public static string ProductsDir { get; private set; } = string.Empty;
    public static string DistributorDir { get; private set; } = string.Empty;
    public static string CatalogDir => Path.Combine(DistributorDir, "imagenes");

    public static void Init(string dbPath)
    {
        var dataDir = Path.GetDirectoryName(dbPath) ?? AppContext.BaseDirectory;
        ProductsDir = Path.Combine(dataDir, "imagenes", "productos");
        DistributorDir = Path.Combine(dataDir, "distribuidor");
        Directory.CreateDirectory(ProductsDir);
        Directory.CreateDirectory(CatalogDir);
    }

    public static string ProductUrl(string file) =>
        $"https://{ProductsHost}/{Uri.EscapeDataString(file)}";

    // version changes on every sync so the WebView does not show an old cached image
    // (the scraper overwrites files that keep the same name).
    public static string CatalogUrl(string file, long version) =>
        $"https://{CatalogHost}/{Uri.EscapeDataString(file)}?v={version}";
}
