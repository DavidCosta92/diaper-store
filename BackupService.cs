using Microsoft.Data.Sqlite;
using panialera.Models;
using panialera.Services;

namespace panialera;

// Daily backup of the database (and of the photos uploaded by hand): a copy with the date
// in its name (e.g. panialera-7-31-26.db), from the configured hour on, optionally only on
// weekdays. A timer calls it periodically; it makes the copy the first time it is due on a
// given day and never duplicates it. It also deletes the oldest copies.
public static class BackupService
{
    private static DateTime? _lastAttempt;

    public static void RunIfDue(string dbPath, string backupDir, int backupHour, int keepCount, bool weekdaysOnly)
    {
        try
        {
            var now = DateTime.Now;

            // Weekdays only (when configured that way).
            if (weekdaysOnly && (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday))
                return;

            // Only from the configured hour on (it catches up if the app is opened later).
            if (now.Hour < backupHour)
                return;

            var destPath = Path.Combine(backupDir, $"panialera-{now:M-d-yy}.db");

            // Today's backup already exists: nothing to do.
            if (File.Exists(destPath))
                return;

            // After a failure, do not retry more than once every 30 minutes.
            if (_lastAttempt.HasValue && (now - _lastAttempt.Value) < TimeSpan.FromMinutes(30))
                return;
            _lastAttempt = now;

            if (!File.Exists(dbPath))
            {
                Logger.Error($"Daily backup: database not found at {dbPath}", null);
                return;
            }

            Directory.CreateDirectory(backupDir);
            BackupTo(dbPath, destPath);
            Logger.Info($"Daily backup created: {destPath}");
            AuditService.Current?.LogFireAndForget(AuditCategory.System, "Backup",
                $"Backup diario creado: {Path.GetFileName(destPath)}", destPath);

            CleanupOldBackups(backupDir, keepCount);
            BackupPhotos(backupDir);
        }
        catch (Exception ex)
        {
            ErrorHub.Report(ex, "Falló el backup diario");
        }
    }

    // Keeps the last 'keepCount' copies and deletes the older ones.
    // keepCount <= 0 => nothing is deleted.
    private static void CleanupOldBackups(string backupDir, int keepCount)
    {
        if (keepCount <= 0)
            return;

        try
        {
            var backups = new DirectoryInfo(backupDir)
                .GetFiles("panialera-*.db")
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            foreach (var old in backups.Skip(keepCount))
            {
                old.Delete();
                Logger.Info($"Old backup deleted: {old.Name}");
            }
        }
        catch (Exception ex)
        {
            ErrorHub.Report(ex, "No se pudieron limpiar backups viejos");
        }
    }

    // Photos uploaded by hand are not in the database: they are copied to a mirror folder
    // inside the backups. Their names are unique (GUID) and never modified, so only the
    // ones not copied yet need copying.
    // The distributor's images are not backed up: a sync downloads them again.
    private static void BackupPhotos(string backupDir)
    {
        try
        {
            if (string.IsNullOrEmpty(AppImages.ProductsDir) || !Directory.Exists(AppImages.ProductsDir))
                return;

            var destDir = Path.Combine(backupDir, "fotos-productos");
            Directory.CreateDirectory(destDir);

            var copied = 0;
            foreach (var source in Directory.GetFiles(AppImages.ProductsDir))
            {
                var dest = Path.Combine(destDir, Path.GetFileName(source));
                if (File.Exists(dest))
                    continue;
                File.Copy(source, dest);
                copied++;
            }

            if (copied > 0)
                Logger.Info($"Photo backup: {copied} new photo(s) copied to {destDir}");
        }
        catch (Exception ex)
        {
            ErrorHub.Report(ex, "No se pudieron respaldar las fotos de los productos");
        }
    }

    // Consistent hot copy through SQLite's backup API
    // (safe even while the app is using the database).
    private static void BackupTo(string dbPath, string destPath)
    {
        using var source = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        source.Open();
        using var destination = new SqliteConnection($"Data Source={destPath}");
        destination.Open();
        source.BackupDatabase(destination);
    }
}
