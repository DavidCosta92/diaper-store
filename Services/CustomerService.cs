using Microsoft.EntityFrameworkCore;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

public class CustomerService(IDbContextFactory<PanialContext> factory, AuditService audit)
{
    private readonly IDbContextFactory<PanialContext> _factory = factory;

    public async Task<List<Customer>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Customers.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Customers.FindAsync(id);
    }

    public async Task<(bool ok, string? error, Customer? customer)> AddAsync(Customer customer)
    {
        customer.Name = customer.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customer.Name))
            return (false, "El nombre es obligatorio.", null);

        await using var db = _factory.CreateDbContext();
        customer.CreatedAt = DateTime.Now;
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.Customers, "Crear",
            $"Se creó el cliente {customer.Name}",
            $"Teléfono: {customer.Phone ?? "(vacío)"}\nDirección: {customer.Address ?? "(vacío)"}",
            AuditEntity.Customer, customer.Id);
        return (true, null, customer);
    }

    // loaded is the customer as the form loaded it. When given, only the fields changed in the form
    // are written: a phone edit applied meanwhile to another field is kept, and on a field both
    // changed, the desktop wins.
    public async Task<(bool ok, string? error)> UpdateAsync(Customer customer, Customer? loaded = null)
    {
        customer.Name = customer.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customer.Name))
            return (false, "El nombre es obligatorio.");

        await using var db = _factory.CreateDbContext();
        var original = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customer.Id);
        if (original != null && loaded != null)
        {
            foreach (var field in RemoteCustomerFields.All)
            {
                var value = RemoteCustomerFields.Get(customer, field);
                if (value == RemoteCustomerFields.Get(loaded, field))
                    SetText(customer, field, RemoteCustomerFields.Get(original, field));
            }
        }
        db.Customers.Update(customer);
        await db.SaveChangesAsync();

        if (original != null)
        {
            var diff = new AuditDiff()
                .Track("Nombre", original.Name, customer.Name)
                .Track("Teléfono", original.Phone, customer.Phone)
                .Track("Dirección", original.Address, customer.Address)
                .Track("Extras", original.Extras, customer.Extras)
                .Track("Opcional 1", original.Optional1, customer.Optional1)
                .Track("Opcional 2", original.Optional2, customer.Optional2)
                .Track("Opcional 3", original.Optional3, customer.Optional3)
                .Track("Opcional 4", original.Optional4, customer.Optional4);
            if (diff.HasChanges)
                await audit.LogAsync(AuditCategory.Customers, "Editar",
                    $"Se editó el cliente {customer.Name}", diff.ToString(), AuditEntity.Customer, customer.Id);
        }
        return (true, null);
    }

    private static void SetText(Customer c, string field, string? value)
    {
        switch (field)
        {
            case RemoteCustomerFields.Name: c.Name = value ?? string.Empty; break;
            case RemoteCustomerFields.Phone: c.Phone = value; break;
            case RemoteCustomerFields.Address: c.Address = value; break;
            case RemoteCustomerFields.Extras: c.Extras = value; break;
            case RemoteCustomerFields.Optional1: c.Optional1 = value; break;
            case RemoteCustomerFields.Optional2: c.Optional2 = value; break;
            case RemoteCustomerFields.Optional3: c.Optional3 = value; break;
            case RemoteCustomerFields.Optional4: c.Optional4 = value; break;
        }
    }

    // Creates a customer made on the phone, in one transaction with the event id.
    public async Task<RemoteCustomerResult> ApplyRemoteCreateAsync(string eventId, RemoteCustomerCreate remote, string createdBy)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var applied = await db.RemoteEventImports.FindAsync(eventId);
        if (applied != null)
            return new RemoteCustomerResult(applied.ResultId, AlreadyApplied: true);

        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var customer = new Customer
        {
            Name = remote.Name.Trim(),
            Phone = Clean(remote.Phone),
            Address = Clean(remote.Address),
            Extras = Clean(remote.Extras),
            Optional1 = Clean(remote.Optional1),
            Optional2 = Clean(remote.Optional2),
            Optional3 = Clean(remote.Optional3),
            Optional4 = Clean(remote.Optional4),
            CreatedAt = DateTime.Now
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var userId = remote.UserId.HasValue && await db.Users.AnyAsync(u => u.Id == remote.UserId.Value) ? remote.UserId : null;
        db.AuditLogs.Add(audit.Entry(AuditCategory.Customers, "Crear desde el celular",
            $"Se creó el cliente {customer.Name}",
            $"Teléfono: {customer.Phone ?? "(vacío)"}\nDirección: {customer.Address ?? "(vacío)"}" +
            (remote.FromContacts ? "\nDesde la agenda del celular" : ""),
            AuditEntity.Customer, customer.Id, userId, createdBy));
        db.RemoteEventImports.Add(new RemoteEventImport
        {
            EventId = eventId,
            EventType = RemoteEventTypes.Customer,
            ResultId = customer.Id,
            AppliedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new RemoteCustomerResult(customer.Id, AlreadyApplied: false);
    }

    // Applies a customer edit made on the phone, only if every value the phone saw still matches.
    // Otherwise the desktop wins: nothing changes and the event is rejected with both values.
    public async Task<RemoteCustomerResult> ApplyRemoteEditAsync(string eventId, RemoteCustomerEdit remote, string createdBy)
    {
        await using var db = _factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var applied = await db.RemoteEventImports.FindAsync(eventId);
        if (applied != null)
            return new RemoteCustomerResult(applied.ResultId, AlreadyApplied: true);

        var customer = await db.Customers.FindAsync(remote.CustomerId)
            ?? throw new EventRejectedException($"El cliente {remote.Name} ya no existe: no se aplicó el cambio.");

        var conflicts = remote.Changes
            .Where(c => !RemoteCustomerFields.Matches(customer, c.Key, c.Value.Seen))
            .Select(c => $"{RemoteCustomerFields.Label(c.Key)}: en el celular se veía " +
                         $"{RemoteCustomerFields.Show(c.Value.Seen.ValueKind == System.Text.Json.JsonValueKind.String ? c.Value.Seen.GetString() : null)} " +
                         $"y en el escritorio es {RemoteCustomerFields.Show(RemoteCustomerFields.Get(customer, c.Key))}")
            .ToList();
        if (conflicts.Count > 0)
            throw new EventRejectedException(
                $"El cliente {customer.Name} cambió mientras tanto y no se aplicó. {string.Join(". ", conflicts)}.");

        var before = new Customer
        {
            Name = customer.Name, Phone = customer.Phone, Address = customer.Address, Extras = customer.Extras,
            Optional1 = customer.Optional1, Optional2 = customer.Optional2,
            Optional3 = customer.Optional3, Optional4 = customer.Optional4
        };
        foreach (var (field, change) in remote.Changes)
            RemoteCustomerFields.Set(customer, field, change.New);

        var diff = new AuditDiff();
        foreach (var field in RemoteCustomerFields.All)
            diff.Track(RemoteCustomerFields.Label(field), RemoteCustomerFields.Get(before, field), RemoteCustomerFields.Get(customer, field));
        if (diff.HasChanges)
        {
            var userId = remote.UserId.HasValue && await db.Users.AnyAsync(u => u.Id == remote.UserId.Value) ? remote.UserId : null;
            db.AuditLogs.Add(audit.Entry(AuditCategory.Customers, "Editar desde el celular",
                $"Se editó el cliente {customer.Name}", diff.ToString(), AuditEntity.Customer, customer.Id, userId, createdBy));
        }

        db.RemoteEventImports.Add(new RemoteEventImport
        {
            EventId = eventId,
            EventType = RemoteEventTypes.CustomerEdit,
            ResultId = customer.Id,
            AppliedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new RemoteCustomerResult(customer.Id, AlreadyApplied: false);
    }

    public async Task<(bool ok, string? error)> DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var customer = await db.Customers.FindAsync(id);
        if (customer == null)
            return (false, "Cliente no encontrado.");

        // A customer who already has sales cannot be deleted (the link is kept).
        if (await db.Sales.AnyAsync(s => s.CustomerId == id))
            return (false, "No podés eliminar un cliente que ya tiene ventas asociadas.");

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.Customers, "Eliminar",
            $"Se eliminó el cliente {customer.Name}", null, AuditEntity.Customer, id);
        return (true, null);
    }

    // The customer's sales (with items) for the history modal.
    public async Task<List<Sale>> GetSalesAsync(int customerId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Sales
            .Include(s => s.Items)
            .Include(s => s.User)
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.Date)
            .ToListAsync();
    }
}
