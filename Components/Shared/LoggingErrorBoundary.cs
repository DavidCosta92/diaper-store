using Microsoft.AspNetCore.Components.Web;

namespace panialera.Components.Shared;

// ErrorBoundary that reports the exception to ErrorHub (file + audit log + toast).
public class LoggingErrorBoundary : ErrorBoundary
{
    protected override Task OnErrorAsync(Exception exception)
    {
        ErrorHub.Report(exception, "Error en pantalla");
        return base.OnErrorAsync(exception);
    }
}
