using System.Runtime.InteropServices;
using System.Windows;
using WpfApp1.Models;

namespace WpfApp1.Services;

public class EdgeSnapManager
{
    private const int SnapMargin = 20;
    private const int CollapsedThickness = 6;

    public void ApplySnap(Window window, AppSettings settings, bool collapse = false)
    {
        var workArea = GetWorkAreaForWindow(window);
        var dpiScale = GetDpiScale(window);
        var dockTop = GetDockTop(window, settings, workArea, dpiScale);
        var dockHeight = settings.ExpandedHeight;

        if (!settings.EnableEdgeSnap || settings.SnapEdge == SnapEdge.None)
        {
            if (settings.WindowLeft is double savedLeft && settings.WindowTop is double savedTop)
            {
                window.Left = savedLeft;
                window.Top = savedTop;
            }

            window.Width = collapse ? CollapsedThickness : settings.ExpandedWidth;
            window.Height = dockHeight;
            return;
        }

        double left, top, width, height;

        switch (settings.SnapEdge)
        {
            case SnapEdge.Left:
                left = workArea.Left / dpiScale;
                top = dockTop;
                width = collapse ? CollapsedThickness : settings.ExpandedWidth;
                height = dockHeight;
                break;
            case SnapEdge.Right:
                width = collapse ? CollapsedThickness : settings.ExpandedWidth;
                height = dockHeight;
                left = (workArea.Right / dpiScale) - width;
                top = dockTop;
                break;
            default:
                left = window.Left;
                top = window.Top;
                width = settings.ExpandedWidth;
                height = dockHeight;
                break;
        }

        window.Left = left;
        window.Top = top;
        window.Width = width;
        window.Height = height;
    }

    public SnapEdge DetectSnapEdge(Window window)
    {
        var workArea = GetWorkAreaForWindow(window);
        var dpiScale = GetDpiScale(window);
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;

        var windowLeft = window.Left * dpiScale;
        var windowRight = windowLeft + width * dpiScale;

        // 左缘进入吸附区：靠近或超出屏幕左边界
        var nearLeft = windowLeft <= workArea.Left + SnapMargin;
        // 右缘进入吸附区：靠近或超出屏幕右边界
        var nearRight = windowRight >= workArea.Right - SnapMargin;

        if (!nearLeft && !nearRight)
        {
            return SnapEdge.None;
        }

        if (nearLeft && !nearRight)
        {
            return SnapEdge.Left;
        }

        if (nearRight && !nearLeft)
        {
            return SnapEdge.Right;
        }

        var leftScore = workArea.Left + SnapMargin - windowLeft;
        var rightScore = windowRight - (workArea.Right - SnapMargin);
        return leftScore >= rightScore ? SnapEdge.Left : SnapEdge.Right;
    }

    public bool IsMouseNearDockEdge(Window window, AppSettings settings, NativePoint mousePos)
    {
        if (!settings.EnableEdgeSnap || settings.SnapEdge == SnapEdge.None)
        {
            return false;
        }

        var workArea = GetWorkAreaForWindow(window);
        var dpiScale = GetDpiScale(window);
        var threshold = settings.ProximityThreshold;
        var dockTop = (int)(GetDockTop(window, settings, workArea, dpiScale) * dpiScale);
        var dockBottom = dockTop + (int)(settings.ExpandedHeight * dpiScale);

        return settings.SnapEdge switch
        {
            SnapEdge.Left => mousePos.X <= workArea.Left + threshold
                && mousePos.Y >= dockTop && mousePos.Y <= dockBottom,
            SnapEdge.Right => mousePos.X >= workArea.Right - threshold
                && mousePos.Y >= dockTop && mousePos.Y <= dockBottom,
            _ => false
        };
    }

    private static double GetDockTop(Window window, AppSettings settings, WorkAreaRect workArea, double dpiScale)
    {
        var height = settings.ExpandedHeight;
        var minTop = workArea.Top / dpiScale;
        var maxTop = (workArea.Bottom / dpiScale) - height;

        if (maxTop < minTop)
        {
            return minTop;
        }

        if (settings.WindowTop is double savedTop)
        {
            return Math.Clamp(savedTop, minTop, maxTop);
        }

        if (window.Top >= minTop && window.Top <= maxTop)
        {
            return window.Top;
        }

        return minTop;
    }

    private static WorkAreaRect GetWorkAreaForWindow(Window window)
    {
        var dpiScale = GetDpiScale(window);
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        var centerX = (int)((window.Left + width / 2) * dpiScale);
        var centerY = (int)((window.Top + height / 2) * dpiScale);
        var hMonitor = MonitorFromPoint(new MonitorNativePoint { X = centerX, Y = centerY }, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(hMonitor, ref info);
        return info.WorkArea;
    }

    private static double GetDpiScale(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget != null)
        {
            return source.CompositionTarget.TransformToDevice.M11;
        }

        return 1.0;
    }

    private const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(MonitorNativePoint pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorNativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public WorkAreaRect Monitor;
        public WorkAreaRect WorkArea;
        public uint Flags;
    }

    internal struct WorkAreaRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }
}
