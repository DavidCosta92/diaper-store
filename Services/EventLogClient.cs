using System.Net.Http.Json;
using System.Text.Json;

namespace panialera.Services;

public sealed record RemoteEvent(string Id, string Type, JsonElement Payload, string Status, int Revision, string CreatedBy);

// The script answered with an error, or with something that is not its API.
public sealed class EventLogException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

// The phone's event log (Google Sheets, through an Apps Script published by the owner).
public interface IEventLogClient
{
    Task<IReadOnlyList<RemoteEvent>> ListPendingAsync(CancellationToken cancellationToken);
    Task<RemoteEvent> ClaimAsync(string id, CancellationToken cancellationToken);
    Task MarkAppliedAsync(string id, string resultId, CancellationToken cancellationToken);
    Task MarkErrorAsync(string id, string message, CancellationToken cancellationToken);

    // Releases a claimed event back to Pendiente without applying it.
    Task RequeueAsync(string id, CancellationToken cancellationToken);
}

public sealed class HttpEventLogClient(HttpClient http, string url, string key) : IEventLogClient
{
    private static readonly string[] PendingStatuses = ["Pendiente", "Aplicando"];
    private const int MaxEvents = 1000;

    public async Task<IReadOnlyList<RemoteEvent>> ListPendingAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            new { key, action = "list", statuses = PendingStatuses, order = "asc", limit = MaxEvents },
            cancellationToken);
        return response.Events ?? throw BadResponse();
    }

    public async Task<RemoteEvent> ClaimAsync(string id, CancellationToken cancellationToken)
    {
        var response = await SendAsync(new { key, action = "claim", id }, cancellationToken);
        return response.Event ?? throw BadResponse();
    }

    public Task MarkAppliedAsync(string id, string resultId, CancellationToken cancellationToken) =>
        SendAsync(new { key, action = "markApplied", id, resultId }, cancellationToken);

    public Task MarkErrorAsync(string id, string message, CancellationToken cancellationToken) =>
        SendAsync(new { key, action = "markError", id, message }, cancellationToken);

    public Task RequeueAsync(string id, CancellationToken cancellationToken) =>
        SendAsync(new { key, action = "requeue", id }, cancellationToken);

    // Google answers the POST with a redirect to the result, which HttpClient follows.
    private async Task<ApiResponse> SendAsync(object body, CancellationToken cancellationToken)
    {
        using var message = await http.PostAsJsonAsync(url, body, cancellationToken);
        message.EnsureSuccessStatusCode();

        ApiResponse? response;
        try
        {
            response = await message.Content.ReadFromJsonAsync<ApiResponse>(cancellationToken);
        }
        catch (JsonException)
        {
            throw BadResponse();
        }

        if (response == null)
            throw BadResponse();
        if (!response.Ok)
            throw new EventLogException(response.Error?.Code ?? "UNKNOWN", response.Error?.Message ?? "Error desconocido.");
        return response;
    }

    private static EventLogException BadResponse() =>
        new("BAD_RESPONSE", "El registro de eventos no devolvió una respuesta válida. Revisá la URL configurada.");

    private sealed record ApiError(string Code, string Message);

    private sealed record ApiResponse(bool Ok, ApiError? Error, RemoteEvent? Event, List<RemoteEvent>? Events);
}
