using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace panialera.Data;

// Used only by the EF Core tools (dotnet ef) to create migrations, since the app is
// WinForms and has no web host with configuration.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PanialContext>
{
    public PanialContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PanialContext>()
            .UseSqlite("Data Source=panialera.db")
            .Options;
        return new PanialContext(options);
    }
}
