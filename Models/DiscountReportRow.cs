namespace panialera.Models;

public class DiscountReportRow
{
    public string Reason { get; set; } = string.Empty;
    public int Count { get; set; }       // how many sales had this reason
    public decimal Total { get; set; }   // sum of the discounts with this reason
}
