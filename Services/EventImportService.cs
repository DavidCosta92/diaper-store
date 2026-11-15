using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using panialera.Models;

namespace panialera.Services;

// An event that can never be applied as it is. It is marked as Error for the phone to fix.
public sealed class EventRejectedException(string message) : Exception(message);

// Applied/Rejected count this run. Drained is false when the queue could not be fully read.
public record ImportRun(int Applied, int Rejected, bool Drained, string? Problem);

// Brings the sales and stock adjustments made on the phone into the database. The desktop app
// stays the only writer of the database: the phone only adds events to its log, and this service
// applies them.
public class EventImportService(
    SaleService sales,
    ProductService products,
    CustomerService customers,
    AuditService audit,
    IEventLogClient? client,
    TimeSpan? pollInterval = null)
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(45);

    // Gives the window time to open, so the first notices are seen.
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    private readonly TimeSpan _pollInterval = pollInterval ?? DefaultPollInterval;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _lastProblem;

    // Sales and stock adjustments saved during the current run (guarded by _gate). Counted when
    // saved, so one whose "applied" mark fails to reach the log still refreshes the screens.
    private int _salesSaved;
    private int _stockSaved;
    private int _productsSaved;
    private int _customersSaved;

    public void Start(CancellationToken cancellationToken = default)
    {
        if (client != null)
            _ = Task.Run(() => LoopAsync(cancellationToken), CancellationToken.None);
    }

    // Applies everything pending. Never throws because of the log or the network: a failed
    // run leaves the events where they were and the next one continues.
    public async Task<ImportRun> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        if (client == null)
            return new ImportRun(0, 0, Drained: true, null);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ProcessQueueAsync(client, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, cancellationToken);
            using var timer = new PeriodicTimer(_pollInterval);
            do
            {
                await RunOnceAsync(cancellationToken);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app is closing.
        }
        catch (Exception ex)
        {
            ErrorHub.Report(ex, "Se detuvo la importación de ventas del celular");
        }
    }

    private async Task<ImportRun> ProcessQueueAsync(IEventLogClient log, CancellationToken cancellationToken)
    {
        _salesSaved = 0;
        _stockSaved = 0;
        _productsSaved = 0;
        _customersSaved = 0;
        var rejected = 0;
        try
        {
            var pending = await log.ListPendingAsync(cancellationToken);
            bool? countOpen = null;
            foreach (var listed in pending)
            {
                // A type this version does not know stays Pendiente for a newer version.
                if (!RemoteEventTypes.IsKnown(listed.Type))
                    continue;

                // With a stock count open, stock adjustments are not even claimed.
                if (listed.Type == RemoteEventTypes.Stock && (countOpen ??= await products.HasOpenStockCountAsync()))
                    continue;

                if (await ProcessEventAsync(log, listed.Id, cancellationToken) == EventOutcome.Rejected)
                    rejected++;
            }

            _lastProblem = null;
            return new ImportRun(Saved, rejected, Drained: true, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ImportRun(Saved, rejected, Drained: false, "Importación cancelada.");
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
            return new ImportRun(Saved, rejected, Drained: false, ex.Message);
        }
        finally
        {
            // A single notice per run, however many events came in.
            if (Saved > 0)
            {
                AppNotices.NotifySalesChanged();
                AppNotices.Raise(SyncedNotice(_salesSaved, _stockSaved, _productsSaved, _customersSaved), string.Empty);
            }
        }
    }

    private int Saved => _salesSaved + _stockSaved + _productsSaved + _customersSaved;

    public static string SyncedNotice(int sales, int stock, int products = 0, int customers = 0) =>
        (sales, stock, products, customers) switch
        {
            ( > 0, 0, 0, 0) => "Ventas sincronizadas",
            (0, > 0, 0, 0) => "Stock sincronizado",
            (0, 0, > 0, 0) => "Productos sincronizados",
            (0, 0, 0, > 0) => "Clientes sincronizados",
            _ => "Cambios del celular sincronizados"
        };

    private async Task<EventOutcome> ProcessEventAsync(IEventLogClient log, string id, CancellationToken cancellationToken)
    {
        RemoteEvent remoteEvent;
        try
        {
            remoteEvent = await log.ClaimAsync(id, cancellationToken);
        }
        catch (EventLogException ex) when (ex.Code is "INVALID_STATE" or "NOT_FOUND")
        {
            // It changed since it was listed (for example, the phone cancelled it).
            return EventOutcome.Skipped;
        }

        try
        {
            return remoteEvent.Type switch
            {
                RemoteEventTypes.Stock => await ApplyStockAsync(log, remoteEvent, cancellationToken),
                RemoteEventTypes.ProductEdit => await ApplyProductAsync(log, remoteEvent,
                    products.ApplyRemoteEditAsync(remoteEvent.Id, Parse<RemoteProductEdit>(remoteEvent, Validate), remoteEvent.CreatedBy),
                    cancellationToken),
                RemoteEventTypes.ProductCreate => await ApplyProductAsync(log, remoteEvent,
                    products.ApplyRemoteCreateAsync(remoteEvent.Id, Parse<RemoteProductCreate>(remoteEvent, Validate), remoteEvent.CreatedBy),
                    cancellationToken),
                RemoteEventTypes.Customer => await ApplyCustomerAsync(log, remoteEvent,
                    customers.ApplyRemoteCreateAsync(remoteEvent.Id, Parse<RemoteCustomerCreate>(remoteEvent, Validate), remoteEvent.CreatedBy),
                    cancellationToken),
                RemoteEventTypes.CustomerEdit => await ApplyCustomerAsync(log, remoteEvent,
                    customers.ApplyRemoteEditAsync(remoteEvent.Id, Parse<RemoteCustomerEdit>(remoteEvent, Validate), remoteEvent.CreatedBy),
                    cancellationToken),
                _ => await ApplySaleAsync(log, remoteEvent, cancellationToken)
            };
        }
        catch (EventRejectedException ex)
        {
            await log.MarkErrorAsync(remoteEvent.Id, ex.Message, cancellationToken);
            await RecordRejectionAsync(remoteEvent, ex.Message);
            return EventOutcome.Rejected;
        }
    }

    private async Task<EventOutcome> ApplySaleAsync(IEventLogClient log, RemoteEvent remoteEvent, CancellationToken cancellationToken)
    {
        var sale = ParseSale(remoteEvent);
        var result = await sales.ApplyRemoteSaleAsync(remoteEvent.Id, sale, remoteEvent.CreatedBy);

        if (!result.AlreadyApplied)
        {
            _salesSaved++;
            // Problems to review are still shown one by one: they need someone to look at them.
            foreach (var warning in result.Warnings)
                AppNotices.Raise($"Revisar venta #{result.SaleId}", warning, isWarning: true);
        }

        await log.MarkAppliedAsync(remoteEvent.Id, result.SaleId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return result.AlreadyApplied ? EventOutcome.Skipped : EventOutcome.Applied;
    }

    private async Task<EventOutcome> ApplyStockAsync(IEventLogClient log, RemoteEvent remoteEvent, CancellationToken cancellationToken)
    {
        var adjustment = ParseStock(remoteEvent);
        var result = await products.ApplyRemoteStockAsync(remoteEvent.Id, adjustment, remoteEvent.CreatedBy);

        // A count started after the list: release the event so it waits as Pendiente.
        if (result.WaitingForStockCount)
        {
            await log.RequeueAsync(remoteEvent.Id, cancellationToken);
            return EventOutcome.Skipped;
        }

        if (!result.AlreadyApplied)
        {
            _stockSaved++;
            foreach (var warning in result.Warnings)
                AppNotices.Raise("Revisar ajuste de stock", warning, isWarning: true);
        }

        await log.MarkAppliedAsync(remoteEvent.Id, result.ProductId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return result.AlreadyApplied ? EventOutcome.Skipped : EventOutcome.Applied;
    }

    private async Task<EventOutcome> ApplyProductAsync(IEventLogClient log, RemoteEvent remoteEvent,
        Task<RemoteProductResult> applying, CancellationToken cancellationToken)
    {
        var result = await applying;
        if (!result.AlreadyApplied)
            _productsSaved++;

        await log.MarkAppliedAsync(remoteEvent.Id, result.ProductId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return result.AlreadyApplied ? EventOutcome.Skipped : EventOutcome.Applied;
    }

    private async Task<EventOutcome> ApplyCustomerAsync(IEventLogClient log, RemoteEvent remoteEvent,
        Task<RemoteCustomerResult> applying, CancellationToken cancellationToken)
    {
        var result = await applying;
        if (!result.AlreadyApplied)
            _customersSaved++;

        await log.MarkAppliedAsync(remoteEvent.Id, result.CustomerId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return result.AlreadyApplied ? EventOutcome.Skipped : EventOutcome.Applied;
    }

    private async Task RecordRejectionAsync(RemoteEvent remoteEvent, string reason)
    {
        var (category, description, notice) = remoteEvent.Type switch
        {
            RemoteEventTypes.Stock => (AuditCategory.Stock,
                $"No se pudo aplicar un ajuste de stock del celular de {remoteEvent.CreatedBy}", "Ajuste del celular sin aplicar"),
            RemoteEventTypes.ProductEdit => (AuditCategory.Products,
                $"No se pudo aplicar un cambio de producto del celular de {remoteEvent.CreatedBy}", "Cambio de producto del celular sin aplicar"),
            RemoteEventTypes.ProductCreate => (AuditCategory.Products,
                $"No se pudo crear un producto del celular de {remoteEvent.CreatedBy}", "Producto del celular sin crear"),
            RemoteEventTypes.Customer => (AuditCategory.Customers,
                $"No se pudo crear un cliente del celular de {remoteEvent.CreatedBy}", "Cliente del celular sin crear"),
            RemoteEventTypes.CustomerEdit => (AuditCategory.Customers,
                $"No se pudo aplicar un cambio de cliente del celular de {remoteEvent.CreatedBy}", "Cambio de cliente del celular sin aplicar"),
            _ => (AuditCategory.Sales,
                $"No se pudo registrar una venta del celular de {remoteEvent.CreatedBy}", "Venta del celular sin registrar")
        };
        await audit.LogAsync(category, "Rechazar", description,
            $"Evento: {remoteEvent.Id}\nMotivo: {reason}",
            userNameOverride: remoteEvent.CreatedBy);
        AppNotices.Raise(notice, reason, isWarning: true);
    }

    // Connection problems are expected and retried, so they only go to the log, once.
    private void ReportFailure(Exception ex)
    {
        if (ex.Message == _lastProblem)
            return;
        _lastProblem = ex.Message;

        var isConnectionProblem = ex is HttpRequestException or TaskCanceledException
            || ex is EventLogException { Code: "BUSY" };
        if (isConnectionProblem)
            Logger.Error("Could not reach the phone's event log; retrying on the next run", ex);
        else
            ErrorHub.Report(ex, "No se pudieron importar las ventas del celular");
    }

    internal static RemoteSale ParseSale(RemoteEvent remoteEvent)
    {
        if (remoteEvent.Type != RemoteEventTypes.Sale)
            throw new EventRejectedException($"Esta versión del escritorio no admite eventos de tipo \"{remoteEvent.Type}\".");

        RemoteSale? sale;
        try
        {
            sale = remoteEvent.Payload.Deserialize<RemoteSale>(PayloadOptions);
        }
        catch (JsonException ex)
        {
            throw new EventRejectedException($"La venta no tiene el formato esperado ({ex.Path}).");
        }

        var problem = sale == null ? "La venta está vacía." : Validate(sale);
        if (problem != null)
            throw new EventRejectedException(problem);
        return sale!;
    }

    private static T Parse<T>(RemoteEvent remoteEvent, Func<T, string?> validate) where T : class
    {
        T? content;
        try
        {
            content = remoteEvent.Payload.Deserialize<T>(PayloadOptions);
        }
        catch (JsonException ex)
        {
            throw new EventRejectedException($"El cambio de producto no tiene el formato esperado ({ex.Path}).");
        }

        var problem = content == null ? "El cambio de producto está vacío." : validate(content);
        if (problem != null)
            throw new EventRejectedException(problem);
        return content!;
    }

    private static string? Validate(RemoteProductEdit edit)
    {
        if (edit.FormatVersion != RemoteProductEdit.CurrentFormatVersion)
            return $"Formato de cambio de producto {edit.FormatVersion} desconocido: actualizá el escritorio.";
        if (edit.ProductId <= 0 || string.IsNullOrWhiteSpace(edit.Name))
            return "El cambio tiene un producto inválido.";
        if (edit.Changes is null or { Count: 0 })
            return $"{edit.Name}: el cambio no trae ningún dato.";

        foreach (var (field, change) in edit.Changes)
        {
            if (change == null)
                return $"{edit.Name}: falta el valor de {field}.";
            var problem = RemoteProductFields.Problem(field, change.New)
                ?? RemoteProductFields.Problem(field, change.Seen);
            if (problem != null)
                return $"{edit.Name}: {RemoteProductFields.Label(field)} {problem}.";
        }
        return null;
    }

    private static string? Validate(RemoteCustomerCreate create)
    {
        if (create.FormatVersion != RemoteCustomerCreate.CurrentFormatVersion)
            return $"Formato de alta de cliente {create.FormatVersion} desconocido: actualizá el escritorio.";
        return string.IsNullOrWhiteSpace(create.Name) ? "El cliente nuevo no tiene nombre." : null;
    }

    private static string? Validate(RemoteCustomerEdit edit)
    {
        if (edit.FormatVersion != RemoteCustomerEdit.CurrentFormatVersion)
            return $"Formato de cambio de cliente {edit.FormatVersion} desconocido: actualizá el escritorio.";
        if (edit.CustomerId <= 0 || string.IsNullOrWhiteSpace(edit.Name))
            return "El cambio tiene un cliente inválido.";
        if (edit.Changes is null or { Count: 0 })
            return $"{edit.Name}: el cambio no trae ningún dato.";

        foreach (var (field, change) in edit.Changes)
        {
            if (change == null)
                return $"{edit.Name}: falta el valor de {field}.";
            var problem = RemoteCustomerFields.Problem(field, change.New)
                ?? (field == RemoteCustomerFields.Name ? null : RemoteCustomerFields.Problem(field, change.Seen));
            if (problem != null)
                return $"{edit.Name}: {RemoteCustomerFields.Label(field)} {problem}.";
        }
        return null;
    }

    private static string? Validate(RemoteProductCreate create)
    {
        if (create.FormatVersion != RemoteProductCreate.CurrentFormatVersion)
            return $"Formato de alta de producto {create.FormatVersion} desconocido: actualizá el escritorio.";
        if (string.IsNullOrWhiteSpace(create.Name))
            return "El producto nuevo no tiene nombre.";
        if (create.PurchasePrice < 0 || create.SalePrice < 0)
            return $"{create.Name}: los precios no pueden ser negativos.";
        if (create.Stock < 0 || create.MinStock < 0)
            return $"{create.Name}: el stock no puede ser negativo.";
        return null;
    }

    internal static RemoteStockAdjustment ParseStock(RemoteEvent remoteEvent)
    {
        RemoteStockAdjustment? adjustment;
        try
        {
            adjustment = remoteEvent.Payload.Deserialize<RemoteStockAdjustment>(PayloadOptions);
        }
        catch (JsonException ex)
        {
            throw new EventRejectedException($"El ajuste de stock no tiene el formato esperado ({ex.Path}).");
        }

        var problem = adjustment == null ? "El ajuste de stock está vacío." : Validate(adjustment);
        if (problem != null)
            throw new EventRejectedException(problem);
        return adjustment!;
    }

    private static string? Validate(RemoteStockAdjustment adjustment)
    {
        if (adjustment.FormatVersion != RemoteStockAdjustment.CurrentFormatVersion)
            return $"Formato de ajuste de stock {adjustment.FormatVersion} desconocido: actualizá el escritorio.";
        if (adjustment.ProductId <= 0 || string.IsNullOrWhiteSpace(adjustment.Name))
            return "El ajuste de stock tiene un producto inválido.";
        if (adjustment.Delta == 0)
            return $"{adjustment.Name}: el ajuste no cambia el stock.";
        if (adjustment.SeenStock + adjustment.Delta != adjustment.NewStock)
            return $"{adjustment.Name}: la diferencia del ajuste no coincide con el stock visto y el nuevo.";
        return null;
    }

    private static string? Validate(RemoteSale sale)
    {
        if (sale.FormatVersion != RemoteSale.CurrentFormatVersion)
            return $"Formato de venta {sale.FormatVersion} desconocido: actualizá el escritorio.";
        if (sale.Date == default)
            return "La venta no tiene fecha.";
        if (sale.Items is null or { Count: 0 })
            return "La venta no tiene productos.";

        foreach (var item in sale.Items)
        {
            if (item == null || item.ProductId <= 0 || string.IsNullOrWhiteSpace(item.Name))
                return "La venta tiene un producto inválido.";
            if (item.Quantity <= 0)
                return $"{item.Name}: la cantidad debe ser mayor a cero.";
            if (item.SalePrice < 0)
                return $"{item.Name}: el precio no puede ser negativo.";
        }

        var discount = sale.Discount?.Amount ?? 0m;
        if (discount < 0 || discount > sale.Subtotal || sale.Discount?.Value < 0)
            return "El descuento de la venta es inválido.";
        if (sale.Total != sale.Subtotal - discount)
            return $"El total de la venta (${sale.Total:F2}) no coincide con sus productos y descuento.";
        return null;
    }

    private enum EventOutcome
    {
        Applied,
        Rejected,
        Skipped
    }
}
