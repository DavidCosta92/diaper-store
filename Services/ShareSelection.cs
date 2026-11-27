using System.Globalization;
using System.Text;

namespace panialera.Services;

// One product chosen to share. Max is the stock cap (null: no cap, as in the distributor catalog).
public sealed class ShareLine
{
    public required string Key { get; init; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public required decimal Price { get; set; }
    public int? Max { get; set; }
    public int Quantity { get; set; } = 1;
}

// Products chosen to share by WhatsApp and the two messages, word for word as the phone app sends
// them: a price list and a ticket with quantities and total. Nothing is recorded: no sale, no stock.
public sealed class ShareSelection
{
    private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");

    private readonly List<ShareLine> _lines = new();

    public IReadOnlyList<ShareLine> Lines => _lines;
    public int Count => _lines.Count;
    public string? Message { get; private set; }

    public bool Contains(string key) => _lines.Any(l => l.Key == key);

    // Without stock it is not added and a notice is left.
    public bool TryAdd(ShareLine line)
    {
        Message = null;
        if (line.Max <= 0)
        {
            Message = $"«{line.Name}» no tiene stock disponible.";
            return false;
        }
        if (Contains(line.Key))
            return false;
        _lines.Add(line);
        return true;
    }

    public void Remove(ShareLine line)
    {
        Message = null;
        _lines.Remove(line);
    }

    public bool TryIncrease(ShareLine line)
    {
        if (line.Max is { } max && line.Quantity >= max)
        {
            Message = $"«{line.Name}» tiene solo {max} unidad(es) en stock.";
            return false;
        }
        Message = null;
        line.Quantity++;
        return true;
    }

    public void Decrease(ShareLine line)
    {
        Message = null;
        if (line.Quantity > 1)
            line.Quantity--;
    }

    public void Clear()
    {
        Message = null;
        _lines.Clear();
    }

    // With new data the selection is kept: prices and stock are updated, and what no longer
    // exists, ran out of stock or has no price is removed with a notice.
    public void Reconcile(Func<string, ShareLine?> current)
    {
        var adjusted = false;
        foreach (var line in _lines.ToList())
        {
            var now = current(line.Key);
            if (now == null || now.Max <= 0)
            {
                _lines.Remove(line);
                adjusted = true;
                continue;
            }
            line.Name = now.Name;
            line.Description = now.Description;
            line.Price = now.Price;
            line.Max = now.Max;
            if (now.Max is { } max && line.Quantity > max)
            {
                line.Quantity = max;
                adjusted = true;
            }
        }
        if (adjusted)
            Message = "Cambiaron los datos y se ajustó la selección.";
    }

    // Message 1: description and price only (a customer asking for prices).
    public string BuildInfoMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Actualmente tenemos estos productos:");
        sb.AppendLine();
        foreach (var l in _lines)
            sb.AppendLine($"• {l.Name}{Desc(l)} – {Money(l.Price)}");
        sb.AppendLine();
        sb.Append("Por favor, confirmame cuál deseas.");
        return sb.ToString();
    }

    // Message 2: ticket with quantities and total.
    public string BuildSaleMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Los productos son:");
        sb.AppendLine();

        decimal total = 0;
        var items = 0;
        foreach (var l in _lines)
        {
            var subtotal = l.Price * l.Quantity;
            total += subtotal;
            items += l.Quantity;

            if (l.Quantity > 1)
                sb.AppendLine($"• {l.Quantity}x {l.Name}{Desc(l)} – {Money(subtotal)} ({l.Quantity} x {Money(l.Price)})");
            else
                sb.AppendLine($"• {l.Name}{Desc(l)} – {Money(l.Price)}");
        }

        sb.AppendLine();
        sb.AppendLine($"El total es de {Money(total)} ({items} {(items == 1 ? "ítem" : "ítems")}).");
        sb.Append("¿Cómo deseas abonarlo?");
        return sb.ToString();
    }

    // Like the phone: pesos with thousands separator and no decimals.
    public static string Money(decimal value) =>
        value < 0 ? "-$" + (-value).ToString("#,##0", Ar) : "$" + value.ToString("#,##0", Ar);

    private static string Desc(ShareLine l) =>
        string.IsNullOrWhiteSpace(l.Description) ? string.Empty : $" – {l.Description}";
}
