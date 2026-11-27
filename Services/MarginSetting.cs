using Microsoft.JSInterop;

namespace panialera.Services;

// The expected margin over the purchase or web price ("Ganancia esperada"), shared by the
// Distributor screen and the product form so both suggest the same price. Remembered on this PC
// (WebView storage); 25% when nothing was set.
public static class MarginSetting
{
    public const string StorageKey = "distribuidor.gananciaPct";
    public const decimal Default = 25m;

    public static async Task<decimal> ReadAsync(IJSRuntime js)
    {
        try
        {
            var saved = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return decimal.TryParse(saved, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var pct) && pct >= 0
                ? pct
                : Default;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or TaskCanceledException)
        {
            return Default;
        }
    }

    // Price plus the margin (e.g. 25% => price * 1.25), rounded to cents.
    public static decimal Apply(decimal price, decimal pct) => Math.Round(price * (1 + pct / 100m), 2);
}
