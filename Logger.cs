using System.Text;

namespace panialera;

// Very simple, fail-safe file logger: it never throws.
// Writes to <app folder>\logs\log-yyyyMMdd.txt (next to the .exe).
public static class Logger
{
    public static string LogDir { get; private set; } = string.Empty;
    private static string _file = string.Empty;
    private static readonly object _lock = new();

    public static void Init()
    {
        try
        {
            LogDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(LogDir);
            _file = Path.Combine(LogDir, $"log-{DateTime.Now:yyyyMMdd}.txt");
        }
        catch
        {
            // If the folder cannot even be created, carry on without a log.
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            if (string.IsNullOrEmpty(_file))
                Init();
            if (string.IsNullOrEmpty(_file))
                return;

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(_file, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never bring the app down.
        }
    }
}
