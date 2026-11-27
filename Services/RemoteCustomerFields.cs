using System.Text.Json;
using panialera.Models;

namespace panialera.Services;

// The customer fields a phone edit can change. All are text; only the name is required. Empty
// means no value, the way the desktop form leaves them.
public static class RemoteCustomerFields
{
    public const string Name = "name";
    public const string Phone = "phone";
    public const string Address = "address";
    public const string Extras = "extras";
    public const string Optional1 = "optional1";
    public const string Optional2 = "optional2";
    public const string Optional3 = "optional3";
    public const string Optional4 = "optional4";

    public static readonly IReadOnlyList<string> All =
        [Name, Phone, Address, Extras, Optional1, Optional2, Optional3, Optional4];

    public static string Label(string field) => field switch
    {
        Name => "Nombre",
        Phone => "Teléfono",
        Address => "Dirección",
        Extras => "Extras",
        Optional1 => "Opcional 1",
        Optional2 => "Opcional 2",
        Optional3 => "Opcional 3",
        Optional4 => "Opcional 4",
        _ => field
    };

    public static string? Problem(string field, JsonElement value) =>
        !All.Contains(field) ? "no es un campo conocido"
        : value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null) ? "debe ser texto"
        : field == Name && string.IsNullOrWhiteSpace(Text(value)) ? "es obligatorio"
        : null;

    public static string? Get(Customer c, string field) => field switch
    {
        Name => c.Name,
        Phone => c.Phone,
        Address => c.Address,
        Extras => c.Extras,
        Optional1 => c.Optional1,
        Optional2 => c.Optional2,
        Optional3 => c.Optional3,
        Optional4 => c.Optional4,
        _ => null
    };

    public static bool Matches(Customer c, string field, JsonElement value) =>
        Optional(Get(c, field)) == Optional(Text(value));

    public static void Set(Customer c, string field, JsonElement value)
    {
        var text = Optional(Text(value))?.Trim();
        switch (field)
        {
            case Name: c.Name = text ?? string.Empty; break;
            case Phone: c.Phone = text; break;
            case Address: c.Address = text; break;
            case Extras: c.Extras = text; break;
            case Optional1: c.Optional1 = text; break;
            case Optional2: c.Optional2 = text; break;
            case Optional3: c.Optional3 = text; break;
            case Optional4: c.Optional4 = text; break;
        }
    }

    public static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(vacío)" : value;

    private static string? Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
