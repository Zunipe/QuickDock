using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using WpfApp1.Models;

namespace WpfApp1.Services;

public class EdgeSummonService : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMbuttondown = 0x0207;

    private readonly EdgeSnapManager _snapManager = new();
    private readonly DispatcherTimer _proximityTimer;
    private LowLevelMouseProc? _mouseProc;
    private IntPtr _hookId = IntPtr.Zero;
    private DateTime _lastMiddleClick = DateTime.MinValue;
    private int _middleClickCount;

    private Window? _window;
    private AppSettings _settings = new();
    private Func<NativeRect>? _getExpandedBounds;
    private Action? _onSummon;
    private Action? _onDismiss;

    public bool IsExpanded { get; set; }

    public EdgeSummonService()
    {
        _proximityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _proximityTimer.Tick += ProximityTimer_Tick;
    }

    public void Start(
        Window window,
        AppSettings settings,
        Func<NativeRect> getExpandedBounds,
        Action onSummon,
        Action onDismiss)
    {
        _window = window;
        _settings = settings;
        _getExpandedBounds = getExpandedBounds;
        _onSummon = onSummon;
        _onDismiss = onDismiss;

        _proximityTimer.Start();
        InstallHook();
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
    }

    public void Stop()
    {
        _proximityTimer.Stop();
        UninstallHook();
    }

    private void ProximityTimer_Tick(object? sender, EventArgs e)
    {
        if (_window == null || _getExpandedBounds == null
            || _settings.SnapEdge == SnapEdge.None || !_settings.EnableEdgeSnap)
        {
            return;
        }

        var mousePos = NativeInput.GetCursorPosition();
        var nearEdge = _snapManager.IsMouseNearDockEdge(_window, _settings, mousePos);

        if (_settings.SummonMode == SummonMode.Proximity)
        {
            if (nearEdge && !IsExpanded)
            {
                _onSummon?.Invoke();
            }
            else if (!nearEdge && IsExpanded && ShouldDismiss(mousePos))
            {
                _onDismiss?.Invoke();
            }
        }
        else if (_settings.SummonMode == SummonMode.ProximityMiddleDoubleClick)
        {
            if (!nearEdge && IsExpanded && ShouldDismiss(mousePos))
            {
                _onDismiss?.Invoke();
            }
        }
    }

    private bool ShouldDismiss(NativePoint mousePos)
    {
        if (_getExpandedBounds == null)
        {
            return false;
        }

        if (NativeInput.IsLeftButtonDown())
        {
            return false;
        }

        return !IsMouseOverExpandedPanel(mousePos, _getExpandedBounds());
    }

    private static bool IsMouseOverExpandedPanel(NativePoint mousePos, NativeRect bounds)
    {
        const int margin = 20;
        return mousePos.X >= bounds.Left - margin
            && mousePos.X <= bounds.Right + margin
            && mousePos.Y >= bounds.Top - margin
            && mousePos.Y <= bounds.Bottom + margin;
    }

    private void InstallHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        _mouseProc = MouseHookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
    }

    private void UninstallHook()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WmMbuttondown
            && _settings.SummonMode == SummonMode.ProximityMiddleDoubleClick
            && _window != null)
        {
            var mousePos = NativeInput.GetCursorPosition();

            if (_snapManager.IsMouseNearDockEdge(_window, _settings, mousePos))
            {
                var now = DateTime.UtcNow;
                if ((now - _lastMiddleClick).TotalMilliseconds < 500)
                {
                    _middleClickCount++;
                    if (_middleClickCount >= 2)
                    {
                        _middleClickCount = 0;
                        System.Windows.Application.Current?.Dispatcher.Invoke(_onSummon);
                    }
                }
                else
                {
                    _middleClickCount = 1;
                }

                _lastMiddleClick = now;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}

public struct NativePoint
{
    public int X;
    public int Y;
}

public struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

internal static class NativeInput
{
    private const int VkLButton = 0x01;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static NativePoint GetCursorPosition()
    {
        GetCursorPos(out var point);
        return point;
    }

    public static bool IsLeftButtonDown()
    {
        return (GetAsyncKeyState(VkLButton) & 0x8000) != 0;
    }
}
