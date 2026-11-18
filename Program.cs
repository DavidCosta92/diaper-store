using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using panialera.Auth;
using panialera.Data;
using panialera.Models;
using panialera.Services;

namespace panialera;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Logger.Init();
        Logger.Info("==================== App starting ====================");
        Logger.Info($"BaseDirectory: {AppContext.BaseDirectory}");

        // Catch every unhandled exception (UI, threads and tasks).
        // They all go through ErrorHub: file log + audit log + notification.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ErrorHub.Report(e.ExceptionObject as Exception, "Error no controlado");
        Application.ThreadException += (_, e) =>
            ErrorHub.Report(e.Exception, "Error no controlado (UI)");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            ErrorHub.Report(e.Exception, "Error en tarea en segundo plano");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        try
        {
            ApplicationConfiguration.Initialize();

            // Stock count PDF (QuestPDF): Community license, free for businesses with
            // annual revenue under USD 1,000,000.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // Self-repair: if a development "static web assets" manifest was left behind
            // (with absolute paths from the development machine) but there is a physical
            // wwwroot next to the .exe, delete it so the app uses the local wwwroot. This
            // avoids the "...\wwwroot does not exist" error after copying a publish over an
            // old folder.
            try
            {
                var localWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                if (Directory.Exists(localWwwroot))
                {
                    foreach (var manifest in Directory.GetFiles(
                                 AppContext.BaseDirectory, "*.staticwebassets.runtime.json"))
                    {
                        File.Delete(manifest);
                        Logger.Info($"Dev manifest deleted (using local wwwroot): {manifest}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Could not clean up the static web assets manifest", ex);
            }

            // Check the WebView2 runtime (Blazor Hybrid needs it).
            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                Logger.Info($"WebView2 runtime found: {version}");
            }
            catch (Exception ex)
            {
                Logger.Error("WebView2 runtime NOT installed", ex);
                MessageBox.Show(
                    "Falta el runtime de Microsoft Edge WebView2, necesario para ejecutar la aplicación.\n\n" +
                    "Instalalo desde:\nhttps://developer.microsoft.com/microsoft-edge/webview2/\n" +
                    "(opción 'Evergreen Standalone Installer') y volvé a abrir la app.",
                    "Cariñitos - Falta WebView2",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();
#if DEBUG
            services.AddBlazorWebViewDeveloperTools();
#endif

            // Send the framework's logs (including component exceptions) to the file.
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning);
                builder.AddProvider(new FileLoggerProvider(LogLevel.Warning));
            });

            // Database path and the rest of the settings come from appsettings.json next to
            // the .exe. Without a path, %APPDATA%\panialera\panialera.db is used.
            var config = AppConfig.Load();
            var dbPath = config.ResolveDbPath();
            var backupDir = config.ResolveBackupPath();
            Logger.Info($"Database path: {dbPath}");
            Logger.Info($"Backup folder: {backupDir}");
            AppImages.Init(dbPath);
            Logger.Info($"Images folder: {AppImages.ProductsDir}");

            services.AddDbContextFactory<PanialContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // Business services
            services.AddSingleton<ProductService>();
            services.AddSingleton<SaleService>();
            services.AddScoped<AuthService>();
            services.AddScoped<CustomerService>();
            services.AddScoped<TodoService>();
            services.AddScoped<ExpenseService>();
            services.AddScoped<StockCountService>();
            services.AddScoped<UnsavedGuard>();
            services.AddScoped<ImageViewer>();
            services.AddSingleton(new DistributorOptions(config.ResolveScraperPath()));
            services.AddSingleton<DistributorService>();
            services.AddSingleton<AuditService>();
            services.AddSingleton(new WhatsAppShare());
            services.AddSingleton(new PhoneSettings(config.ResolveAreaCode()));
            services.AddSingleton<PasswordHasher>();
            services.AddSingleton<EventImportService>(sp => new EventImportService(
                sp.GetRequiredService<SaleService>(),
                sp.GetRequiredService<ProductService>(),
                new CustomerService(sp.GetRequiredService<IDbContextFactory<PanialContext>>(), sp.GetRequiredService<AuditService>()),
                sp.GetRequiredService<AuditService>(),
                config.IsEventLogConfigured()
                    ? new HttpEventLogClient(new HttpClient { Timeout = TimeSpan.FromSeconds(60) },
                        config.EventLogUrl!.Trim(), config.EventLogKey!.Trim())
                    : null,
                config.ResolveEventLogPollInterval()));

            // Authentication / authorization
            services.AddSingleton<AppAuthenticationStateProvider>();
            services.AddSingleton<AuthenticationStateProvider>(
                sp => sp.GetRequiredService<AppAuthenticationStateProvider>());
            services.AddAuthorizationCore();

            var provider = services.BuildServiceProvider();
            var audit = provider.GetRequiredService<AuditService>();
            AuditService.Current = audit;

            // Apply migrations and make sure the admin user exists at startup.
            using (var scope = provider.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PanialContext>>();
                using var db = factory.CreateDbContext();
                Logger.Info("Applying migrations...");
                db.Database.Migrate();
                Logger.Info("Migrations applied. Checking the admin user...");

                var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
                auth.EnsureSeed();
                Logger.Info("Admin user checked/created.");

                scope.ServiceProvider.GetRequiredService<PhoneSettings>()
                    .PublishAsync(factory).GetAwaiter().GetResult();

                // Delete to-dos completed more than 60 days ago.
                try
                {
                    var todoService = scope.ServiceProvider.GetRequiredService<TodoService>();
                    var deleted = todoService.CleanupCompletedAsync().GetAwaiter().GetResult();
                    Logger.Info($"Old completed to-dos deleted: {deleted}");
                    if (deleted > 0)
                        audit.LogAsync(AuditCategory.System, "Limpieza",
                            $"Se eliminaron {deleted} tareas completadas hace más de 60 días").GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ErrorHub.Report(ex, "No se pudo limpiar tareas viejas");
                }

                // Audit log retention (configurable; 0 = keep everything).
                try
                {
                    var days = config.ResolveAuditRetentionDays();
                    var deleted = audit.CleanupAsync(days).GetAwaiter().GetResult();
                    if (deleted > 0)
                    {
                        Logger.Info($"Audit log: {deleted} entries older than {days} days deleted");
                        audit.LogAsync(AuditCategory.System, "Limpieza",
                            $"Se eliminaron {deleted} entradas de la bitácora con más de {days} días").GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    ErrorHub.Report(ex, "No se pudo limpiar la bitácora");
                }

                audit.LogAsync(AuditCategory.System, "Inicio", "Se inició la aplicación").GetAwaiter().GetResult();
            }

            provider.GetRequiredService<EventImportService>().Start();

            Logger.Info("Opening the main window...");
            Application.Run(new MainForm(provider, dbPath, backupDir,
                config.ResolveBackupHour(), config.ResolveBackupKeepCount(),
                config.ResolveBackupWeekdaysOnly()));
            Logger.Info("==================== App closed normally ====================");
        }
        catch (Exception ex)
        {
            Logger.Error("Fatal error at startup", ex);
            MessageBox.Show(
                "Ocurrió un error al iniciar la aplicación:\n\n" + ex.Message +
                "\n\nSe guardó el detalle en:\n" + Logger.LogDir,
                "Cariñitos - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
