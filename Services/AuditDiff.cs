namespace panialera.Services;

// Builds the "field: before → after" details of an edit for the audit log.
// Field names and values are shown to the user, so they are passed in Spanish.
public class AuditDiff
{
    private readonly List<string> _lines = new();

    public AuditDiff Track(string field, object? before, object? after)
    {
        var b = Format(before);
        var a = Format(after);
        if (b != a)
            _lines.Add($"{field}: {b} → {a}");
        return this;
    }

    public bool HasChanges => _lines.Count > 0;

    public override string ToString() => string.Join("\n", _lines);

    private static string Format(object? v) => v switch
    {
        null => "(vacío)",
        string s when string.IsNullOrWhiteSpace(s) => "(vacío)",
        decimal m => $"${m:F2}",
        DateTime d => d.ToString("dd/MM/yyyy"),
        _ => v.ToString() ?? "(vacío)"
    };
}
