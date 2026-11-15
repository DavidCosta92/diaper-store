using Microsoft.Extensions.Logging;

namespace panialera;

// Logging provider that forwards the framework's messages (including unhandled exceptions
// in Blazor components) to Logger's file.
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minLevel;

    public FileLoggerProvider(LogLevel minLevel = LogLevel.Warning)
    {
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _minLevel);

    public void Dispose() { }

    private sealed class FileLogger(string category, LogLevel minLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var text = $"[{category}] {message}";
            if (logLevel >= LogLevel.Error)
            {
                // App/Blazor exceptions => ErrorHub (file + audit log + toast).
                // EF Core ones only go to the file: recording a database error in the
                // database could start an error loop.
                if (exception != null && !category.StartsWith("Microsoft.EntityFrameworkCore"))
                    ErrorHub.Report(exception, text);
                else
                    Logger.Error(text, exception);
            }
            else
                Logger.Info($"[{logLevel}] {text}");
        }
    }
}
