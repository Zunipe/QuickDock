using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp1.Models;

namespace WpfApp1.Services;

public static class IconHelper
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const int IldTransparent = 0x00000001;
    private const int ShilJumbo = 4;
    private const int ShilExtraLarge = 2;
    private const int ShilLarge = 0;
    private const int HighResExtractSize = 256;

    private static readonly Guid ImageListGuid = new("46EB5926-582E-4017-9FEF-E26379B7160C");

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGELISTDRAWPARAMS
    {
        public int cbSize;
        public IntPtr himl;
        public int i;
        public IntPtr hdcDst;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public int xBitmap;
        public int yBitmap;
        public int rgbBk;
        public int rgbFg;
        public int fStyle;
        public int dwRop;
        public int fState;
        public int Frame;
        public int crEffect;
    }

    [ComImport]
    [Guid("46EB5926-582E-4017-9FEF-E26379B7160C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig]
        int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);

        [PreserveSig]
        int ReplaceIcon(int i, IntPtr hicon, ref int pi);

        [PreserveSig]
        int SetOverlayImage(int iImage, int iOverlay);

        [PreserveSig]
        int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);

        [PreserveSig]
        int AddMasked(IntPtr hbmImage, int crMask, ref int pi);

        [PreserveSig]
        int Draw(ref IMAGELISTDRAWPARAMS pimldp);

        [PreserveSig]
        int Remove(int i);

        [PreserveSig]
        int GetIcon(int i, int flags, out IntPtr picon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        IntPtr[] phicon,
        int[]? piconid,
        uint nIcons,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static ImageSource GetIcon(LaunchItem item, int size = 96)
    {
        var renderSize = Math.Max(size, 96);

        if (!string.IsNullOrEmpty(item.CustomIconPath) && File.Exists(item.CustomIconPath))
        {
            return LoadImage(item.CustomIconPath, renderSize);
        }

        return GetSystemIcon(item.Path, item.ItemType, renderSize);
    }

    public static ImageSource GetSystemIcon(string path, LaunchItemType type, int size = 96)
    {
        try
        {
            if (type == LaunchItemType.Folder)
            {
                return GetFolderIcon(path, size);
            }

            if (File.Exists(path))
            {
                return GetShellIcon(path, isDirectory: false, size);
            }
        }
        catch
        {
            // fall through to default
        }

        return type == LaunchItemType.Folder ? GetFolderIcon(path, size) : GetDefaultIcon(size);
    }

    private static ImageSource GetFolderIcon(string path, int size)
    {
        if (Directory.Exists(path))
        {
            var icon = TryGetShellFileIcon(path, isDirectory: true, size);
            if (icon != null)
            {
                return icon;
            }
        }

        var fallbackPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var fallbackIcon = TryGetShellFileIcon(fallbackPath, isDirectory: true, size);
        return fallbackIcon ?? GetDefaultIcon(size);
    }

    private static ImageSource GetShellIcon(string path, bool isDirectory, int size)
    {
        var icon = TryGetShellFileIcon(path, isDirectory, size);
        if (icon != null)
        {
            return icon;
        }

        if (!isDirectory)
        {
            icon = TryExtractHighResIcon(path, size);
            if (icon != null)
            {
                return icon;
            }
        }

        return isDirectory ? GetFolderIcon(path, size) : GetDefaultIcon(size);
    }

    private static ImageSource? TryGetShellFileIcon(string path, bool isDirectory, int size)
    {
        var iconIndex = TryGetSystemIconIndex(path, isDirectory);
        if (iconIndex.HasValue)
        {
            foreach (var listId in new[] { ShilJumbo, ShilExtraLarge, ShilLarge })
            {
                var imageListIcon = TryGetIconFromImageList(iconIndex.Value, listId, size);
                if (imageListIcon != null)
                {
                    return imageListIcon;
                }
            }
        }

        return TryGetLegacyShellIcon(path, isDirectory, size);
    }

    private static int? TryGetSystemIconIndex(string path, bool isDirectory)
    {
        var shfi = new SHFILEINFO();
        var flags = ShgfiSysIconIndex;
        var attributes = 0u;

        if (isDirectory)
        {
            flags |= ShgfiUseFileAttributes;
            attributes = FileAttributeDirectory;
            path = string.IsNullOrWhiteSpace(path) ? "Folder" : path;
        }
        else if (!File.Exists(path))
        {
            return null;
        }

        return SHGetFileInfo(
            path,
            attributes,
            ref shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            flags) != IntPtr.Zero
            ? shfi.iIcon
            : null;
    }

    private static ImageSource? TryGetIconFromImageList(int iconIndex, int imageListId, int size)
    {
        var guid = ImageListGuid;
        if (SHGetImageList(imageListId, ref guid, out var imageList) != 0)
        {
            return null;
        }

        if (imageList.GetIcon(iconIndex, IldTransparent, out var hIcon) != 0 || hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hIcon).Clone();
            return ToImageSource(icon, size);
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static ImageSource? TryGetLegacyShellIcon(string path, bool isDirectory, int size)
    {
        var shfi = new SHFILEINFO();
        var flags = ShgfiIcon | ShgfiLargeIcon;
        var attributes = 0u;

        if (isDirectory)
        {
            flags |= ShgfiUseFileAttributes;
            attributes = FileAttributeDirectory;
            path = string.IsNullOrWhiteSpace(path) ? "Folder" : path;
        }
        else if (!File.Exists(path))
        {
            return null;
        }

        var result = SHGetFileInfo(
            path,
            attributes,
            ref shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            flags);

        if (result == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(shfi.hIcon).Clone();
            return ToImageSource(icon, size);
        }
        finally
        {
            DestroyIcon(shfi.hIcon);
        }
    }

    private static ImageSource? TryExtractHighResIcon(string path, int size)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".exe" or ".dll" or ".ico"))
        {
            return null;
        }

        IntPtr[] icons = [IntPtr.Zero];
        var count = PrivateExtractIcons(path, 0, HighResExtractSize, HighResExtractSize, icons, null, 1, 0);
        if (count == 0 || icons[0] == IntPtr.Zero)
        {
            count = PrivateExtractIcons(path, 0, 96, 96, icons, null, 1, 0);
        }

        if (count == 0 || icons[0] == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(icons[0]).Clone();
            return ToImageSource(icon, size);
        }
        finally
        {
            DestroyIcon(icons[0]);
        }
    }

    private static ImageSource LoadImage(string path, int size)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.DecodePixelWidth = Math.Max(size, HighResExtractSize);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static ImageSource ToImageSource(System.Drawing.Icon icon, int size)
    {
        using var bitmap = icon.ToBitmap();
        return BitmapToImageSource(ScaleBitmap(bitmap, size));
    }

    private static System.Drawing.Bitmap ScaleBitmap(System.Drawing.Bitmap source, int size)
    {
        if (source.Width == size && source.Height == size)
        {
            return (System.Drawing.Bitmap)source.Clone();
        }

        var scaled = new System.Drawing.Bitmap(size, size);
        using (var graphics = System.Drawing.Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, 0, 0, size, size);
        }

        return scaled;
    }

    private static ImageSource BitmapToImageSource(System.Drawing.Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
            bitmap.Dispose();
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static ImageSource GetDefaultIcon(int size)
    {
        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            dc.DrawRectangle(
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(99, 102, 241)),
                null,
                new Rect(0, 0, size, size));
            var text = new FormattedText(
                "?",
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size * 0.5,
                System.Windows.Media.Brushes.White,
                1.0);
            dc.DrawText(text, new System.Windows.Point((size - text.Width) / 2, (size - text.Height) / 2));
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(drawingVisual);
        rtb.Freeze();
        return rtb;
    }

    public static LaunchItemType DetectType(string path)
    {
        if (Directory.Exists(path))
        {
            return LaunchItemType.Folder;
        }

        if (File.Exists(path))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".exe" or ".bat" or ".cmd" or ".msi" or ".lnk")
            {
                return LaunchItemType.Application;
            }

            return LaunchItemType.File;
        }

        return LaunchItemType.Application;
    }

    public static string GetDisplayName(string path)
    {
        if (Directory.Exists(path))
        {
            return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        if (File.Exists(path))
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        return path;
    }
}
