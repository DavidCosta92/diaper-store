using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Web.WebView2.Core;
using panialera.Components;

namespace panialera;

public class MainForm : Form
{
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly int _backupHour;
    private readonly int _backupKeepCount;
    private readonly bool _backupWeekdaysOnly;
    private System.Windows.Forms.Timer? _backupTimer;

    public MainForm(IServiceProvider services, string dbPath, string backupDir,
        int backupHour, int backupKeepCount, bool backupWeekdaysOnly)
    {
        _dbPath = dbPath;
        _backupDir = backupDir;
        _backupHour = backupHour;
        _backupKeepCount = backupKeepCount;
        _backupWeekdaysOnly = backupWeekdaysOnly;

        Text = "Cariñitos";
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        // Window and taskbar icon.
        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
            if (File.Exists(icoPath))
                Icon = new Icon(icoPath);
        }
        catch (Exception ex)
        {
            Logger.Error("Could not load the window icon", ex);
        }

        try
        {
            Logger.Info("Creating BlazorWebView...");
            var blazor = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = Path.Combine("wwwroot", "index.html"),
                Services = services
            };

            blazor.BlazorWebViewInitialized += (_, e) =>
            {
                Logger.Info("BlazorWebView initialized.");

                // Expose the image folders to the WebView (see AppImages).
                var core = e.WebView.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(AppImages.ProductsHost,
                    AppImages.ProductsDir, CoreWebView2HostResourceAccessKind.Allow);
                core.SetVirtualHostNameToFolderMapping(AppImages.CatalogHost,
                    AppImages.CatalogDir, CoreWebView2HostResourceAccessKind.Allow);
            };

            blazor.RootComponents.Add<Routes>("#app");
            blazor.RootComponents.Add<HeadOutlet>("head::after");

            Controls.Add(blazor);
            Logger.Info("BlazorWebView added to the window.");
        }
        catch (Exception ex)
        {
            Logger.Error("Error creating BlazorWebView", ex);
            throw;
        }

        // Daily backup (from the configured hour). Checked every 5 minutes and once at
        // startup (in case the app is opened after that hour).
        BackupService.RunIfDue(_dbPath, _backupDir, _backupHour, _backupKeepCount, _backupWeekdaysOnly);
        _backupTimer = new System.Windows.Forms.Timer { Interval = 300_000 };
        _backupTimer.Tick += (_, _) =>
            BackupService.RunIfDue(_dbPath, _backupDir, _backupHour, _backupKeepCount, _backupWeekdaysOnly);
        _backupTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing with the X asks for confirmation.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            var r = MessageBox.Show(
                this,
                "¿Seguro que querés cerrar la aplicación?",
                "Cariñitos",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (r != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _backupTimer?.Stop();
        _backupTimer?.Dispose();
        base.OnFormClosed(e);
    }
}
