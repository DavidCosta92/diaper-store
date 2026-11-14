using Microsoft.EntityFrameworkCore;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

public class ExpenseService(IDbContextFactory<PanialContext> factory, AuditService audit)
{
    private readonly IDbContextFactory<PanialContext> _factory = factory;

    private static string PaymentLabel(PaymentType t) => t == PaymentType.MercadoPago ? "Mercado Pago" : "Efectivo";

    public async Task<List<Expense>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Expenses
            .OrderByDescending(e => e.Date)
            .ToListAsync();
    }

    // Expenses within a date range (for the reports).
    public async Task<List<Expense>> GetByRangeAsync(DateTime? from, DateTime? to)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.Expenses.AsQueryable();

        if (from.HasValue)
        {
            var fromDate = from.Value.Date;
            query = query.Where(e => e.Date >= fromDate);
        }
        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            query = query.Where(e => e.Date < toExclusive);
        }

        return await query.OrderByDescending(e => e.Date).ToListAsync();
    }

    public async Task<(bool ok, string? error)> AddAsync(Expense expense)
    {
        expense.Name = expense.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expense.Name))
            return (false, "El nombre es obligatorio.");
        if (expense.Amount <= 0)
            return (false, "El valor debe ser mayor a 0.");

        await using var db = _factory.CreateDbContext();
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.Expenses, "Crear",
            $"Se cargó el egreso \"{expense.Name}\" por ${expense.Amount:F2}",
            $"Fecha: {expense.Date:dd/MM/yyyy}\nPago: {PaymentLabel(expense.PaymentType)}" +
            (string.IsNullOrWhiteSpace(expense.Description) ? "" : $"\nDescripción: {expense.Description}"),
            AuditEntity.Expense, expense.Id);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> UpdateAsync(Expense expense)
    {
        expense.Name = expense.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expense.Name))
            return (false, "El nombre es obligatorio.");
        if (expense.Amount <= 0)
            return (false, "El valor debe ser mayor a 0.");

        await using var db = _factory.CreateDbContext();
        var original = await db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == expense.Id);
        db.Expenses.Update(expense);
        await db.SaveChangesAsync();

        if (original != null)
        {
            var diff = new AuditDiff()
                .Track("Nombre", original.Name, expense.Name)
                .Track("Valor", original.Amount, expense.Amount)
                .Track("Fecha", original.Date.Date, expense.Date.Date)
                .Track("Pago", PaymentLabel(original.PaymentType), PaymentLabel(expense.PaymentType))
                .Track("Descripción", original.Description, expense.Description);
            if (diff.HasChanges)
                await audit.LogAsync(AuditCategory.Expenses, "Editar",
                    $"Se editó el egreso \"{expense.Name}\"", diff.ToString(), AuditEntity.Expense, expense.Id);
        }
        return (true, null);
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var expense = await db.Expenses.FindAsync(id);
        if (expense != null)
        {
            db.Expenses.Remove(expense);
            await db.SaveChangesAsync();

            await audit.LogAsync(AuditCategory.Expenses, "Eliminar",
                $"Se eliminó el egreso \"{expense.Name}\" (${expense.Amount:F2})", null, AuditEntity.Expense, id);
        }
    }
}
