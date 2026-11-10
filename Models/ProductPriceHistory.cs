namespace panialera.Models;

public class ProductPriceHistory
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.Now;
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
}
