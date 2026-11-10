using System.ComponentModel.DataAnnotations.Schema;

namespace panialera.Models;

public class Sale
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public bool Voided { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Optional discount on the whole sale.
    public decimal DiscountValue { get; set; }          // raw value entered (20 = 20% or 2000 = $2000)
    public bool DiscountIsPercentage { get; set; }      // true => DiscountValue is a percentage
    public decimal DiscountAmount { get; set; }         // actual discount in money
    public string? DiscountReason { get; set; }         // reason (rounding, adjustment, etc.)

    public List<SaleItem> Items { get; set; } = new();

    // Subtotal (sum of the items) and total after the discount.
    [NotMapped]
    public decimal Subtotal => Items.Sum(i => i.SalePrice * i.Quantity);

    [NotMapped]
    public decimal Total => Subtotal - DiscountAmount;

    // Registered from the phone (applied from its event log). Filled in by SaleService.GetAllAsync.
    [NotMapped]
    public bool FromPhone { get; set; }
}
