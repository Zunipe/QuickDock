using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;

namespace WpfApp1.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private MainWindow? _mainWindow;
    private bool _disposed;

    public TrayIconService()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = AppIconHelper.CreateIcon(32),
            Text = "QuickDock",
            Visible = true
        };

        _notifyIcon.MouseUp += OnNotifyIconMouseUp;
        _notifyIcon.DoubleClick += (_, _) => RestoreMainWindow();
    }

    public event Action? ExitRequested;

    public static ImageSource GetWindowIconSource()
    {
        using var bitmap = AppIconHelper.Render(32);
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    public void SetMainWindow(MainWindow window)
    {
        _mainWindow = window;
    }

    public void RestoreMainWindow()
    {
        if (_mainWindow == null)
        {
            return;
        }

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            _mainWindow.RestoreFromTray();
        });
    }

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        System.Windows.Application.Current?.Dispatcher.Invoke(ShowTrayContextMenu);
    }

    private void ShowTrayContextMenu()
    {
        var target = _mainWindow ?? System.Windows.Application.Current?.MainWindow;
        if (target == null)
        {
            return;
        }

        var menu = new WpfContextMenu();

        var showItem = new WpfMenuItem { Header = "显示窗口" };
        showItem.Click += (_, _) => RestoreMainWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new WpfSeparator());

        var exitItem = new WpfMenuItem { Header = "退出" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        menu.PlacementTarget = target;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.MouseUp -= OnNotifyIconMouseUp;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
