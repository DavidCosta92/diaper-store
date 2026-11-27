namespace panialera.Models;

// A value the desktop publishes in the database for the phone app, which cannot read
// appsettings.json. The desktop writes it at startup from its configuration.
public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public static class AppSettingKeys
{
    // Area code assumed for phone numbers written without one (DEC-028 of the phone app).
    public const string DefaultAreaCode = "DefaultAreaCode";
}
