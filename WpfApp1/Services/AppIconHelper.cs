using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace WpfApp1.Services;

public static class AppIconHelper
{
    private static readonly Color Background = Color.FromArgb(26, 29, 39);
    private static readonly Color BorderColor = Color.FromArgb(46, 51, 72);
    private static readonly Color Accent = Color.FromArgb(99, 102, 241);
    private static readonly Color AccentHighlight = Color.FromArgb(129, 140, 248);

    public static Bitmap Render(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        var inset = size / 7f;
        var bounds = new RectangleF(inset, inset, size - inset * 2, size - inset * 2);
        var cornerRadius = size / 5.5f;

        using (var backgroundPath = CreateRoundedRect(bounds, cornerRadius))
        {
            using var backgroundBrush = new SolidBrush(Background);
            using var borderPen = new Pen(BorderColor, Math.Max(1f, size / 28f));
            graphics.FillPath(backgroundBrush, backgroundPath);
            graphics.DrawPath(borderPen, backgroundPath);
        }

        DrawLightning(graphics, size);
        return bitmap;
    }

    public static Icon CreateIcon(int size = 32)
    {
        using var bitmap = Render(size);
        var handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static void SaveIconFile(string path, params int[] sizes)
    {
        var iconSizes = sizes.Length > 0 ? sizes : [16, 32, 48, 256];
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)iconSizes.Length);

        var imageData = new List<byte[]>();
        var offset = 6 + 16 * iconSizes.Length;

        foreach (var size in iconSizes)
        {
            using var bitmap = Render(size);
            using var pngStream = new MemoryStream();
            bitmap.Save(pngStream, ImageFormat.Png);
            var pngBytes = pngStream.ToArray();
            imageData.Add(pngBytes);

            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(pngBytes.Length);
            writer.Write(offset);

            offset += pngBytes.Length;
        }

        foreach (var pngBytes in imageData)
        {
            writer.Write(pngBytes);
        }
    }

    private static void DrawLightning(Graphics graphics, int size)
    {
        var points = new[]
        {
            new PointF(size * 0.54f, size * 0.24f),
            new PointF(size * 0.40f, size * 0.50f),
            new PointF(size * 0.49f, size * 0.50f),
            new PointF(size * 0.43f, size * 0.76f),
            new PointF(size * 0.64f, size * 0.46f),
            new PointF(size * 0.52f, size * 0.46f),
        };

        using var shadowPath = new GraphicsPath();
        shadowPath.AddPolygon(OffsetPoints(points, size * 0.015f, size * 0.02f));
        using var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
        graphics.FillPath(shadowBrush, shadowPath);

        using var boltPath = new GraphicsPath();
        boltPath.AddPolygon(points);
        using var gradientBrush = new LinearGradientBrush(
            new RectangleF(size * 0.38f, size * 0.22f, size * 0.28f, size * 0.56f),
            AccentHighlight,
            Accent,
            LinearGradientMode.Vertical);
        graphics.FillPath(gradientBrush, boltPath);
    }

    private static PointF[] OffsetPoints(IReadOnlyList<PointF> points, float dx, float dy)
    {
        return points.Select(point => new PointF(point.X + dx, point.Y + dy)).ToArray();
    }

    private static GraphicsPath CreateRoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
