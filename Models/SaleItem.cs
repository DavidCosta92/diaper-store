namespace panialera.Models;

// A sold product. Name, description and prices are a snapshot taken at sale time, so
// past sales do not change when the product is edited or deleted.
public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal SalePrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public PaymentType PaymentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Sale? Sale { get; set; }
}
