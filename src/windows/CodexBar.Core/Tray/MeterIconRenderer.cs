using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexBar.Core.Tray;

public static class MeterIconRenderer
{
    public static Icon Render(TrayDisplayModel model)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var back = model.IsStale
            ? Color.FromArgb(120, 148, 163, 184)
            : Color.FromArgb(255, 210, 218, 232);
        var fill = model.IsStale
            ? Color.FromArgb(255, 100, 116, 139)
            : Color.FromArgb(255, 37, 99, 235);
        using var backBrush = new SolidBrush(back);
        using var fillBrush = new SolidBrush(fill);

        var bars = new[]
        {
            new Rectangle(7, 18, 4, 8),
            new Rectangle(14, 12, 4, 14),
            new Rectangle(21, 6, 4, 20)
        };
        var percent = Math.Clamp(model.Percent, 0, 100) / 100.0;
        foreach (var bar in bars)
        {
            graphics.FillRoundedRectangle(backBrush, bar, 2);
            if (percent > 0)
            {
                var fillHeight = Math.Max(1, (int)Math.Round(bar.Height * percent));
                var fillRect = new Rectangle(bar.X, bar.Bottom - fillHeight, bar.Width, fillHeight);
                graphics.FillRoundedRectangle(fillBrush, fillRect, 2);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
