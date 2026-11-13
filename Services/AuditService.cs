using Microsoft.EntityFrameworkCore;
using panialera.Auth;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

// Audit log: records every relevant operation.
// Singleton: code without DI (backup, global error handlers) reaches it through Current.
public class AuditService(IDbContextFactory<PanialContext> factory, AppAuthenticationStateProvider auth)
{
    public static AuditService? Current { get; set; }

    // Records an operation. Never throws: on failure it only goes to the file log.
    public async Task LogAsync(
        AuditCategory category,
        string action,
        string description,
        string? details = null,
        string? entityType = null,
        int? entityId = null,
        int? userIdOverride = null,
        string? userNameOverride = null)
    {
        try
        {
            await using var db = factory.CreateDbContext();
            db.AuditLogs.Add(Entry(category, action, description, details, entityType, entityId,
                userIdOverride, userNameOverride));
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not write to the audit log: [{category}] {description}", ex);
        }
    }

    // Builds an entry without saving it, for operations that must store it in their own
    // transaction. With a name override the entry belongs to that person, so the user logged
    // in on this PC is not used as the id.
    public AuditLog Entry(
        AuditCategory category,
        string action,
        string description,
        string? details = null,
        string? entityType = null,
        int? entityId = null,
        int? userIdOverride = null,
        string? userNameOverride = null) => new()
    {
        Date = DateTime.Now,
        UserId = userIdOverride ?? (userNameOverride == null ? auth.CurrentUserId : null),
        UserName = userNameOverride ?? auth.CurrentUserName,
        Category = category,
        Action = action,
        Description = description,
        Details = string.IsNullOrWhiteSpace(details) ? null : details,
        EntityType = entityType,
        EntityId = entityId
    };

    // For synchronous code (timers, global handlers): does not block.
    public void LogFireAndForget(
        AuditCategory category, string action, string description, string? details = null,
        string? entityType = null, int? entityId = null)
    {
        _ = Task.Run(() => LogAsync(category, action, description, details, entityType, entityId));
    }

    public async Task<List<AuditLog>> SearchAsync(
        DateTime? from, DateTime? to, IReadOnlyCollection<AuditCategory> categories, string? text, int max)
    {
        await using var db = factory.CreateDbContext();
        var q = db.AuditLogs.AsQueryable();

        if (from.HasValue)
        {
            var fromDate = from.Value.Date;
            q = q.Where(a => a.Date >= fromDate);
        }
        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            q = q.Where(a => a.Date < toExclusive);
        }
        q = q.Where(a => categories.Contains(a.Category));

        if (!string.IsNullOrWhiteSpace(text))
        {
            var like = $"%{text.Trim()}%";
            q = q.Where(a =>
                EF.Functions.Like(a.Description, like)
                || EF.Functions.Like(a.Action, like)
                || (a.Details != null && EF.Functions.Like(a.Details, like))
                || (a.UserName != null && EF.Functions.Like(a.UserName, like)));
        }

        return await q.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id)
            .Take(max)
            .ToListAsync();
    }

    // History of a single record (e.g. a product). entityType is an AuditEntity value.
    public async Task<List<AuditLog>> GetForEntityAsync(string entityType, int entityId)
    {
        await using var db = factory.CreateDbContext();
        return await db.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Date).ThenByDescending(a => a.Id)
            .ToListAsync();
    }

    // Deletes entries older than N days (N <= 0 => keep everything).
    public async Task<int> CleanupAsync(int days)
    {
        if (days <= 0)
            return 0;
        await using var db = factory.CreateDbContext();
        var limit = DateTime.Now.AddDays(-days);
        return await db.AuditLogs.Where(a => a.Date < limit).ExecuteDeleteAsync();
    }
}
