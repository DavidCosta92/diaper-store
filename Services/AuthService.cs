using Microsoft.EntityFrameworkCore;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

public class AuthService(IDbContextFactory<PanialContext> factory, PasswordHasher hasher, AuditService audit)
{
    private readonly IDbContextFactory<PanialContext> _factory = factory;
    private readonly PasswordHasher _hasher = hasher;

    public async Task<User?> ValidateLoginAsync(string name, string password)
    {
        await using var db = _factory.CreateDbContext();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Name == name);
        var ok = user != null && _hasher.Verify(password, user.PasswordHash);

        if (ok)
        {
            await audit.LogAsync(AuditCategory.Session, "Inicio de sesión",
                $"{user!.Name} inició sesión", null, AuditEntity.User, user.Id,
                userIdOverride: user.Id, userNameOverride: user.Name);
            return user;
        }

        await audit.LogAsync(AuditCategory.Session, "Login fallido",
            $"Intento de inicio de sesión fallido para \"{name}\"",
            user == null ? "Usuario inexistente" : "Contraseña incorrecta",
            userNameOverride: name);
        return null;
    }

    // Called on logout (before the current user is cleared).
    public Task LogLogoutAsync() =>
        audit.LogAsync(AuditCategory.Session, "Cierre de sesión", "Se cerró la sesión");

    public async Task<List<User>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Users.OrderBy(u => u.Name).ToListAsync();
    }

    public async Task<(bool ok, string? error)> CreateUserAsync(string name, string password, UserRole role)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return (false, "El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(password))
            return (false, "La contraseña es obligatoria.");

        await using var db = _factory.CreateDbContext();
        if (await db.Users.AnyAsync(u => u.Name == name))
            return (false, "Ya existe un usuario con ese nombre.");

        var user = new User
        {
            Name = name,
            PasswordHash = _hasher.Hash(password),
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.Users, "Crear",
            $"Se creó el usuario {name}", $"Rol: {role.DisplayName()}", AuditEntity.User, user.Id);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> UpdateUserAsync(int id, UserRole role, string? newPassword)
    {
        await using var db = _factory.CreateDbContext();
        var user = await db.Users.FindAsync(id);
        if (user == null)
            return (false, "Usuario no encontrado.");

        // Never leave the app without an administrator.
        if (user.Role == UserRole.Admin && role != UserRole.Admin)
        {
            var admins = await db.Users.CountAsync(u => u.Role == UserRole.Admin);
            if (admins <= 1)
                return (false, "No podés quitar el rol al último administrador.");
        }

        var diff = new AuditDiff().Track("Rol", user.Role.DisplayName(), role.DisplayName());
        user.Role = role;
        var passwordReset = !string.IsNullOrWhiteSpace(newPassword);
        if (passwordReset)
            user.PasswordHash = _hasher.Hash(newPassword!);

        await db.SaveChangesAsync();

        if (diff.HasChanges || passwordReset)
        {
            var details = diff.ToString();
            if (passwordReset)
                details = string.IsNullOrEmpty(details) ? "Contraseña restablecida" : details + "\nContraseña restablecida";
            await audit.LogAsync(AuditCategory.Users, "Editar",
                $"Se editó el usuario {user.Name}", details, AuditEntity.User, id);
        }
        return (true, null);
    }

    public async Task<(bool ok, string? error)> DeleteUserAsync(int id, int? currentUserId)
    {
        await using var db = _factory.CreateDbContext();
        var user = await db.Users.FindAsync(id);
        if (user == null)
            return (false, "Usuario no encontrado.");
        if (currentUserId.HasValue && user.Id == currentUserId.Value)
            return (false, "No podés eliminar tu propio usuario.");
        if (user.Role == UserRole.Admin && await db.Users.CountAsync(u => u.Role == UserRole.Admin) <= 1)
            return (false, "No podés eliminar el último administrador.");

        // A user who already registered sales cannot be deleted (the history is kept).
        if (await db.Sales.AnyAsync(s => s.UserId == id))
            return (false, "No podés eliminar un usuario que ya registró ventas. Se conserva el historial.");

        db.Users.Remove(user);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.Users, "Eliminar",
            $"Se eliminó el usuario {user.Name}", $"Rol: {user.Role.DisplayName()}", AuditEntity.User, id);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> ChangePasswordAsync(int userId, string current, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return (false, "La nueva contraseña es obligatoria.");

        await using var db = _factory.CreateDbContext();
        var user = await db.Users.FindAsync(userId);
        if (user == null)
            return (false, "Usuario no encontrado.");
        if (!_hasher.Verify(current, user.PasswordHash))
            return (false, "La contraseña actual es incorrecta.");

        user.PasswordHash = _hasher.Hash(newPassword);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditCategory.Users, "Cambio de contraseña",
            $"{user.Name} cambió su contraseña", null, AuditEntity.User, userId);
        return (true, null);
    }

    // Makes sure an administrator always exists. With none, it creates admin / 123
    // (a known default: change it after the first login).
    public void EnsureSeed()
    {
        using var db = _factory.CreateDbContext();
        if (!db.Users.Any(u => u.Role == UserRole.Admin))
        {
            db.Users.Add(new User
            {
                Name = "admin",
                PasswordHash = _hasher.Hash("123"),
                Role = UserRole.Admin
            });
            db.SaveChanges();
            audit.LogAsync(AuditCategory.Users, "Crear",
                "Se creó el usuario administrador por defecto (admin)", null,
                userNameOverride: "Sistema").GetAwaiter().GetResult();
        }
    }
}
