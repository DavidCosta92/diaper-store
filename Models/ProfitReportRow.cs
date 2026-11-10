namespace panialera.Models;

public class ProfitReportRow
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalRevenue { get; set; }

    public decimal TotalProfit => TotalRevenue - TotalCost;
    public decimal AvgPurchasePrice => UnitsSold > 0 ? TotalCost / UnitsSold : 0;
    public decimal AvgSalePrice => UnitsSold > 0 ? TotalRevenue / UnitsSold : 0;
    public decimal ProfitPerUnit => UnitsSold > 0 ? TotalProfit / UnitsSold : 0;
}
