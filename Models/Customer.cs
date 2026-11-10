namespace panialera.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Extras { get; set; }
    public string? Optional1 { get; set; }
    public string? Optional2 { get; set; }
    public string? Optional3 { get; set; }
    public string? Optional4 { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
