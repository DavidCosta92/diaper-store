using System.Text.Json;

namespace panialera;

// User-editable settings, in a single appsettings.json next to the .exe.
// Every adjustable setting of the app lives here.
public class AppConfig
{
    // Full path to the database file. When empty, the default
    // %APPDATA%\panialera\panialera.db is used.
    public string? DatabasePath { get; set; }

    // Folder for the daily backup copies (with the date in the name).
    // When empty, %APPDATA%\panialera\backups is used.
    public string? BackupPath { get; set; }

    // Hour (0-23) from which the daily backup runs. Default 18.
    public int? BackupHour { get; set; }

    // Number of backup copies to keep; older ones are deleted.
    // 0 or negative = keep all. Default 30.
    public int? BackupKeepCount { get; set; }

    // When true, the backup only runs Monday to Friday. Default true.
    public bool? BackupWeekdaysOnly { get; set; }

    // Days the audit log entries are kept.
    // 0 or negative = keep everything. Default 365.
    public int? AuditRetentionDays { get; set; }

    // Path to the distributor catalog's Scrapper.exe. When empty, the one shipped with the
    // installer is used ("scrapper" subfolder next to the .exe).
    public string? ScraperPath { get; set; }

    // URL and desktop key of the phone's event log (Apps Script). While either is empty, the
    // app does not look for sales made on the phone.
    public string? EventLogUrl { get; set; }
    public string? EventLogKey { get; set; }

    // Seconds between checks of the event log. Minimum 15. Default 45.
    public int? EventLogPollSeconds { get; set; }

    // Area code assumed for customer phones written without one (to compare numbers and open
    // WhatsApp). Default 264. The phone app takes it from the database.
    public string? DefaultAreaCode { get; set; }

    private const int MinEventLogPollSeconds = 15;
    private const int DefaultEventLogPollSeconds = 45;
    private const int DefaultBackupHour = 18;
    private const int DefaultBackupKeepCount = 30;
    private const bool DefaultBackupWeekdaysOnly = true;
    private const int DefaultAuditRetentionDays = 365;

    private static string ConfigFilePath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    // Old settings file, migrated if the user still has it.
    private static string LegacyConfigFilePath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            var legacy = File.Exists(LegacyConfigFilePath) ? Read(LegacyConfigFilePath) : null;

            // 1) appsettings.json (main source).
            if (File.Exists(ConfigFilePath))
            {
                var cfg = Read(ConfigFilePath) ?? new AppConfig();

                // If an inherited appsettings.json has no paths but the old config.json does,
                // take them so the settings are not lost.
                if (legacy != null)
                {
                    if (string.IsNullOrWhiteSpace(cfg.DatabasePath) && !string.IsNullOrWhiteSpace(legacy.DatabasePath))
                        cfg.DatabasePath = legacy.DatabasePath;
                    if (string.IsNullOrWhiteSpace(cfg.BackupPath) && !string.IsNullOrWhiteSpace(legacy.BackupPath))
                        cfg.BackupPath = legacy.BackupPath;
                }

                cfg.FillDefaults();
                cfg.Save(); // normalizes the file so every option is visible
                Logger.Info($"Settings loaded from {ConfigFilePath}");
                return cfg;
            }

            // 2) Migrate from the old config.json if it exists.
            if (legacy != null)
            {
                legacy.FillDefaults();
                legacy.Save();
                Logger.Info($"Settings migrated from config.json to {ConfigFilePath}");
                return legacy;
            }

            // 3) First run: create appsettings.json with the defaults.
            var created = new AppConfig
            {
                DatabasePath = DefaultDbPath(),
                BackupPath = DefaultBackupPath(),
                BackupHour = DefaultBackupHour,
                BackupKeepCount = DefaultBackupKeepCount,
                BackupWeekdaysOnly = DefaultBackupWeekdaysOnly,
                AuditRetentionDays = DefaultAuditRetentionDays,
                EventLogPollSeconds = DefaultEventLogPollSeconds,
                DefaultAreaCode = panialera.Services.PhoneNumbers.DefaultAreaCode
            };
            created.Save();
            Logger.Info($"appsettings.json created at {ConfigFilePath}");
            return created;
        }
        catch (Exception ex)
        {
            Logger.Error("Could not read appsettings.json; using defaults", ex);
        }

        return new AppConfig();
    }

    private static AppConfig? Read(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private void FillDefaults()
    {
        BackupHour ??= DefaultBackupHour;
        BackupKeepCount ??= DefaultBackupKeepCount;
        BackupWeekdaysOnly ??= DefaultBackupWeekdaysOnly;
        AuditRetentionDays ??= DefaultAuditRetentionDays;
        EventLogPollSeconds ??= DefaultEventLogPollSeconds;
        DefaultAreaCode ??= panialera.Services.PhoneNumbers.DefaultAreaCode;
    }

    // Only digits, without the leading 0; the default if it is empty.
    public string ResolveAreaCode()
    {
        var digits = new string((DefaultAreaCode ?? string.Empty).Where(char.IsDigit).ToArray()).TrimStart('0');
        return digits.Length == 0 ? panialera.Services.PhoneNumbers.DefaultAreaCode : digits;
    }

    // Effective database path (the configured one, or the default).
    public string ResolveDbPath()
    {
        var path = string.IsNullOrWhiteSpace(DatabasePath) ? DefaultDbPath() : DatabasePath.Trim();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        return path;
    }

    // Effective backup folder (the configured one, or the default).
    public string ResolveBackupPath()
    {
        var dir = string.IsNullOrWhiteSpace(BackupPath) ? DefaultBackupPath() : BackupPath.Trim();
        Directory.CreateDirectory(dir);
        return dir;
    }

    // Effective backup hour (0-23). Out of range => default.
    public int ResolveBackupHour()
    {
        var h = BackupHour ?? DefaultBackupHour;
        return (h >= 0 && h <= 23) ? h : DefaultBackupHour;
    }

    // Number of copies to keep (<= 0 => keep all).
    public int ResolveBackupKeepCount() => BackupKeepCount ?? DefaultBackupKeepCount;

    public bool ResolveBackupWeekdaysOnly() => BackupWeekdaysOnly ?? DefaultBackupWeekdaysOnly;

    public int ResolveAuditRetentionDays() => AuditRetentionDays ?? DefaultAuditRetentionDays;

    public bool IsEventLogConfigured() =>
        !string.IsNullOrWhiteSpace(EventLogUrl) && !string.IsNullOrWhiteSpace(EventLogKey);

    public TimeSpan ResolveEventLogPollInterval() =>
        TimeSpan.FromSeconds(Math.Max(MinEventLogPollSeconds, EventLogPollSeconds ?? DefaultEventLogPollSeconds));

    public string ResolveScraperPath() =>
        string.IsNullOrWhiteSpace(ScraperPath)
            ? Path.Combine(AppContext.BaseDirectory, "scrapper", "Scrapper.exe")
            : ScraperPath.Trim();

    private void Save()
    {
        // If the folder is read-only (e.g. installed under Program Files), reading the
        // settings must not fail: the save is skipped.
        try
        {
            File.WriteAllText(ConfigFilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logger.Error("Could not save appsettings.json (read-only folder?)", ex);
        }
    }

    public static string DefaultDbPath()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "panialera");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "panialera.db");
    }

    public static string DefaultBackupPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "panialera", "backups");
    }
}
