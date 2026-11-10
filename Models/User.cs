namespace panialera.Models;

public enum UserRole
{
    Admin = 0,
    Employee = 1,
    Manager = 2
}

public static class UserRoleExtensions
{
    // Name shown in the UI.
    public static string DisplayName(this UserRole r) => r switch
    {
        UserRole.Admin => "Admin",
        UserRole.Employee => "Empleado",
        UserRole.Manager => "Jefe",
        _ => r.ToString()
    };
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
