using Microsoft.EntityFrameworkCore;
using panialera.Data;
using panialera.Models;

namespace panialera.Services;

// The area code for phone numbers written without one, from appsettings.json (DefaultAreaCode).
public sealed record PhoneSettings(string AreaCode)
{
    // Copies the area code to the database so the phone app uses the same one.
    public async Task PublishAsync(IDbContextFactory<PanialContext> factory)
    {
        await using var db = factory.CreateDbContext();
        var setting = await db.AppSettings.FindAsync(AppSettingKeys.DefaultAreaCode);
        if (setting?.Value == AreaCode)
            return;
        if (setting == null)
            db.AppSettings.Add(new AppSetting { Key = AppSettingKeys.DefaultAreaCode, Value = AreaCode });
        else
            setting.Value = AreaCode;
        await db.SaveChangesAsync();
    }
}
