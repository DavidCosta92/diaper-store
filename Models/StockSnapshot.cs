namespace panialera.Models;

// Daily photo of a product's stock and prices: the first time the app is open on a day it
// saves one row per product. It gives the stock history that did not exist before (days out of
// stock, stock that does not move, value of the goods on a past date).
// ProductId has no FK on purpose: like sales and counts, the history outlives the product.
public class StockSnapshot
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int ProductId { get; set; }
    public int Stock { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
}
