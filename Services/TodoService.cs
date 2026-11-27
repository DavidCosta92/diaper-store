using Microsoft.EntityFrameworkCore;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

// To-dos ("Pendientes").
public class TodoService(IDbContextFactory<PanialContext> factory, AuditService audit)
{
    private readonly IDbContextFactory<PanialContext> _factory = factory;

    // Raised after any change, so every open view (the Pendientes page and the sticky note)
    // shows the same list.
    public event Action? Changed;

    private void Notify() => Changed?.Invoke();

    // The ones still to do, highest priority first, then the newest.
    public async Task<List<TodoItem>> GetOpenAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.TodoItems
            .Include(t => t.Customer)
            .Where(t => !t.Completed)
            .OrderBy(t => t.Priority)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<TodoItem>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.TodoItems
            .Include(t => t.Customer)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<(bool ok, string? error)> AddAsync(TodoItem todo)
    {
        todo.Title = todo.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(todo.Title))
            return (false, "El título es obligatorio.");

        await using var db = _factory.CreateDbContext();
        todo.CreatedAt = DateTime.Now;
        db.TodoItems.Add(todo);
        await db.SaveChangesAsync();

        var customer = await CustomerName(db, todo.CustomerId);
        await audit.LogAsync(AuditCategory.Todos, "Crear",
            $"Se creó la tarea #{todo.Id} \"{todo.Title}\"",
            $"Prioridad: {todo.Priority.DisplayName()}" +
            (customer == null ? "" : $"\nCliente: {customer}") +
            (string.IsNullOrWhiteSpace(todo.Description) ? "" : $"\nDescripción: {todo.Description}"),
            AuditEntity.TodoItem, todo.Id);
        Notify();
        return (true, null);
    }

    public async Task<(bool ok, string? error)> UpdateAsync(TodoItem todo)
    {
        todo.Title = todo.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(todo.Title))
            return (false, "El título es obligatorio.");

        await using var db = _factory.CreateDbContext();
        var original = await db.TodoItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == todo.Id);
        var stored = await db.TodoItems.FirstOrDefaultAsync(t => t.Id == todo.Id);
        if (stored == null)
            return (false, "La tarea ya no existe.");

        // Only what the form edits: completing or reopening is done apart (the sticky note can
        // complete it while the Pendientes page has it open for editing).
        stored.Title = todo.Title;
        stored.Description = todo.Description;
        stored.Priority = todo.Priority;
        stored.CustomerId = todo.CustomerId;
        await db.SaveChangesAsync();

        if (original != null)
        {
            var diff = new AuditDiff()
                .Track("Título", original.Title, todo.Title)
                .Track("Descripción", original.Description, todo.Description)
                .Track("Prioridad", original.Priority.DisplayName(), todo.Priority.DisplayName())
                .Track("Cliente", await CustomerName(db, original.CustomerId), await CustomerName(db, todo.CustomerId));
            if (diff.HasChanges)
                await audit.LogAsync(AuditCategory.Todos, "Editar",
                    $"Se editó la tarea #{todo.Id} \"{todo.Title}\"", diff.ToString(), AuditEntity.TodoItem, todo.Id);
        }
        Notify();
        return (true, null);
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var todo = await db.TodoItems.FindAsync(id);
        if (todo != null)
        {
            db.TodoItems.Remove(todo);
            await db.SaveChangesAsync();

            await audit.LogAsync(AuditCategory.Todos, "Eliminar",
                $"Se eliminó la tarea #{id} \"{todo.Title}\"", null, AuditEntity.TodoItem, id);
            Notify();
        }
    }

    // Marks as completed / reopens. Completing stores the date (for the 60-day cleanup).
    public async Task SetCompletedAsync(int id, bool completed)
    {
        await using var db = _factory.CreateDbContext();
        var todo = await db.TodoItems.FindAsync(id);
        if (todo == null)
            return;

        todo.Completed = completed;
        todo.CompletedAt = completed ? DateTime.Now : null;
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.Todos, completed ? "Completar" : "Reabrir",
            completed
                ? $"Se completó la tarea #{id} \"{todo.Title}\""
                : $"Se reabrió la tarea #{id} \"{todo.Title}\"",
            null, AuditEntity.TodoItem, id);
        Notify();
    }

    // Deletes the to-dos completed more than N days ago (by completion date).
    // Called at startup.
    public async Task<int> CleanupCompletedAsync(int days = 60)
    {
        await using var db = _factory.CreateDbContext();
        var limit = DateTime.Now.AddDays(-days);
        var old = await db.TodoItems
            .Where(t => t.Completed && t.CompletedAt != null && t.CompletedAt < limit)
            .ToListAsync();

        if (old.Count > 0)
        {
            db.TodoItems.RemoveRange(old);
            await db.SaveChangesAsync();
            Notify();
        }
        return old.Count;
    }

    private static async Task<string?> CustomerName(PanialContext db, int? customerId) =>
        customerId.HasValue ? (await db.Customers.FindAsync(customerId.Value))?.Name : null;
}
