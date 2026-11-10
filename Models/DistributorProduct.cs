namespace panialera.Models;

// A product from the distributor's catalog (imported from the scraper). Read-only:
// every sync deletes everything and loads it again.
public class DistributorProduct
{
    public int Id { get; set; }
    public string ItemId { get; set; } = string.Empty;   // internal id on the distributor's site
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal? WebPrice { get; set; }
    public bool InStock { get; set; }
    public string? Alert { get; set; }

    // File name inside the catalog images folder.
    public string? ImageFile { get; set; }
}

// A record of each catalog sync.
public class DistributorSync
{
    public int Id { get; set; }
    public DateTime GeneratedAt { get; set; }   // when the scraper wrote the file
    public DateTime ImportedAt { get; set; }    // when it was imported into the database
    public int Count { get; set; }
    public string? UserName { get; set; }
}
