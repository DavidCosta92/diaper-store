using System.Globalization;
using System.Text.Json;
using panialera.Models;

namespace panialera.Services;

// The product fields a phone edit can change, and how each one is compared and written. The phone
// sends text fields as strings (code and category may be null) and the rest as numbers.
public static class RemoteProductFields
{
    public const string Code = "code";
    public const string Name = "name";
    public const string Category = "category";
    public const string Description = "description";
    public const string PurchasePrice = "purchasePrice";
    public const string SalePrice = "salePrice";
    public const string MinStock = "minStock";

    public static readonly IReadOnlyList<string> All =
        [Code, Name, Category, Description, PurchasePrice, SalePrice, MinStock];

    public static string Label(string field) => field switch
    {
        Code => "Código",
        Name => "Nombre",
        Category => "Categoría",
        Description => "Descripción",
        PurchasePrice => "Precio compra",
        SalePrice => "Precio venta",
        MinStock => "Stock mínimo",
        _ => field
    };

    // Null when the value is valid for the field; otherwise what is wrong with it.
    public static string? Problem(string field, JsonElement value) => field switch
    {
        Code or Category => value.ValueKind is JsonValueKind.String or JsonValueKind.Null ? null : "debe ser texto",
        Name => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? null
            : "es obligatorio",
        Description => value.ValueKind is JsonValueKind.String or JsonValueKind.Null ? null : "debe ser texto",
        PurchasePrice or SalePrice => value.ValueKind == JsonValueKind.Number && value.GetDecimal() >= 0
            ? null
            : "debe ser un número mayor o igual a cero",
        MinStock => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) && n >= 0
            ? null
            : "debe ser un entero mayor o igual a cero",
        _ => "no es un campo conocido"
    };

    public static bool Matches(Product p, string field, JsonElement value) => field switch
    {
        Code => Optional(p.Code) == Optional(Text(value)),
        Category => Optional(p.Category) == Optional(Text(value)),
        Name => p.Name == (Text(value) ?? string.Empty),
        Description => p.Description == (Text(value) ?? string.Empty),
        PurchasePrice => p.PurchasePrice == value.GetDecimal(),
        SalePrice => p.SalePrice == value.GetDecimal(),
        MinStock => p.MinStock == value.GetInt32(),
        _ => false
    };

    public static void Set(Product p, string field, JsonElement value)
    {
        switch (field)
        {
            case Code: p.Code = Optional(Text(value)); break;
            case Category: p.Category = Optional(Text(value)); break;
            case Name: p.Name = Text(value)!.Trim(); break;
            case Description: p.Description = Text(value) ?? string.Empty; break;
            case PurchasePrice: p.PurchasePrice = value.GetDecimal(); break;
            case SalePrice: p.SalePrice = value.GetDecimal(); break;
            case MinStock: p.MinStock = value.GetInt32(); break;
        }
    }

    public static string Show(Product p, string field) => field switch
    {
        Code => p.Code ?? "(vacío)",
        Category => p.Category ?? "(vacío)",
        Name => p.Name,
        Description => p.Description,
        PurchasePrice => Money(p.PurchasePrice),
        SalePrice => Money(p.SalePrice),
        MinStock => p.MinStock.ToString(CultureInfo.InvariantCulture),
        _ => string.Empty
    };

    // Only for values that passed Problem.
    public static string Show(string field, JsonElement value)
    {
        var p = new Product();
        Set(p, field, value);
        return Show(p, field);
    }

    private static string Money(decimal value) => $"${value:F2}";

    private static string? Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // Code and category: empty means no value, like the desktop form saves them.
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
