namespace panialera.Models;

// The enum order is the priority order (High first when sorting ascending).
public enum Priority
{
    High = 0,
    Medium = 1,
    Low = 2
}

public static class PriorityExtensions
{
    // Name shown in the UI.
    public static string DisplayName(this Priority p) => p switch
    {
        Priority.High => "Alta",
        Priority.Medium => "Media",
        Priority.Low => "Baja",
        _ => p.ToString()
    };
}

// A to-do ("pendiente").
public class TodoItem
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Related customer (optional).
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
