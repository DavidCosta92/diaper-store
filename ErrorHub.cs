using System.Runtime.CompilerServices;
using panialera.Models;
using panialera.Services;

namespace panialera;

public record ErrorNotice(string Context, string Message, DateTime Date);

// Single place to report exceptions: writes them to the file log, records them in the
// audit log (Errors category) and notifies the UI (toast).
// The context text is shown to the user, so callers pass it in Spanish.
public static class ErrorHub
{
    public static event Action<ErrorNotice>? ErrorOccurred;

    // Avoids reporting the same exception twice (it can arrive through several paths).
    private static readonly ConditionalWeakTable<Exception, object> Reported = new();

    [ThreadStatic] private static bool _reporting;

    public static void Report(Exception? ex, string context)
    {
        if (ex is null)
        {
            Logger.Error(context, null);
            return;
        }

        if (!Reported.TryAdd(ex, new object()))
            return;

        Logger.Error(context, ex);

        // Re-entrancy guard (e.g. recording the error raises another error).
        if (_reporting)
            return;

        try
        {
            _reporting = true;
            AuditService.Current?.LogFireAndForget(
                AuditCategory.Errors,
                ex.GetType().Name,
                $"{context}: {ex.Message}",
                ex.ToString());
            ErrorOccurred?.Invoke(new ErrorNotice(context, ex.Message, DateTime.Now));
        }
        catch
        {
            // Reporting must never break the app.
        }
        finally
        {
            _reporting = false;
        }
    }
}
