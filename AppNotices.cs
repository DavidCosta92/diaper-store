namespace panialera;

public record AppNotice(string Title, string Message, DateTime Date, bool IsWarning);

// Things that happen in the background and the screens need to know about.
// Handlers can run on any thread.
public static class AppNotices
{
    public static event Action<AppNotice>? Raised;
    public static event Action? SalesChanged;

    public static void Raise(string title, string message, bool isWarning = false) =>
        Raised?.Invoke(new AppNotice(title, message, DateTime.Now, isWarning));

    public static void NotifySalesChanged() => SalesChanged?.Invoke();
}
