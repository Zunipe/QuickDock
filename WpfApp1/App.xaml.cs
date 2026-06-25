using System.IO;
using System.Windows;
using WpfApp1.Services;

namespace WpfApp1;

public partial class App : System.Windows.Application
{
    private TrayIconService? _trayIconService;

    public TrayIconService TrayIconService => _trayIconService
        ?? throw new InvalidOperationException("Tray icon service is not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--generate-icon"))
        {
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            AppIconHelper.SaveIconFile(Path.Combine(projectRoot, "Assets", "AppIcon.ico"));
            Shutdown();
            return;
        }

        if (StartupHelper.HandleStartupCommandLine(e.Args))
        {
            Shutdown();
            return;
        }

        _trayIconService = new TrayIconService();
        _trayIconService.ExitRequested += () => Shutdown();

        var mainWindow = new MainWindow();
        _trayIconService.SetMainWindow(mainWindow);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        base.OnExit(e);
    }
}
