namespace panialera.Models;

public class Product
{
    public int Id { get; set; }

    // External item code (e.g. the distributor's). Alphanumeric, may contain slashes or
    // dashes. It should be unique but that is not enforced.
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Stock { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public int MinStock { get; set; }

    // Image uploaded by hand (file name inside the product images folder). Without one, the
    // distributor's catalog image with the same code is used.
    public string? ImageFile { get; set; }

    // Last change: who and when.
    public DateTime? LastUpdatedAt { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public User? LastUpdatedByUser { get; set; }
}
