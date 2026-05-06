using CodexBar.Tray;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CodexBar.Tests;

[TestClass]
public sealed class MeterIconRendererTests
{
    [TestMethod]
    public void RendersNonEmptyIcon()
    {
        using var icon = MeterIconRenderer.Render(new TrayDisplayModel("CodexBar", 72, false));

        Assert.IsTrue(icon.Width > 0);
        Assert.IsTrue(icon.Height > 0);
    }

    [TestMethod]
    public void ZeroPercentDoesNotDrawActiveFill()
    {
        using var icon = MeterIconRenderer.Render(new TrayDisplayModel("CodexBar", 0, false));
        using var bitmap = icon.ToBitmap();

        Assert.AreEqual(0, CountActiveFillPixels(bitmap));
        Assert.IsTrue(CountVisiblePixels(bitmap) > 0);
    }

    [TestMethod]
    public void FullPercentDrawsActiveFill()
    {
        using var icon = MeterIconRenderer.Render(new TrayDisplayModel("CodexBar", 100, false));
        using var bitmap = icon.ToBitmap();

        Assert.IsTrue(CountActiveFillPixels(bitmap) > 20);
    }

    [TestMethod]
    public void StaleIconUsesMutedFill()
    {
        using var icon = MeterIconRenderer.Render(new TrayDisplayModel("CodexBar", 100, true));
        using var bitmap = icon.ToBitmap();

        Assert.AreEqual(0, CountActiveFillPixels(bitmap));
        Assert.IsTrue(CountMutedFillPixels(bitmap) > 20);
    }

    [TestMethod]
    public void TrayHostStaysHiddenUntilFirstUpdate()
    {
        using var host = new TrayIconHost(() => { }, () => { }, () => { });
        var notifyIcon = GetNotifyIcon(host);

        Assert.IsFalse(notifyIcon.Visible);
        Assert.IsNull(notifyIcon.Icon);

        host.Update(new TrayDisplayModel("CodexBar", 72, false));

        Assert.IsTrue(notifyIcon.Visible);
        Assert.IsNotNull(notifyIcon.Icon);
    }

    private static NotifyIcon GetNotifyIcon(TrayIconHost host)
    {
        var field = typeof(TrayIconHost).GetField("notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return (NotifyIcon)field.GetValue(host)!;
    }

    private static int CountVisiblePixels(Bitmap bitmap) =>
        CountPixels(bitmap, color => color.A > 0);

    private static int CountActiveFillPixels(Bitmap bitmap) =>
        CountPixels(bitmap, color => color.A > 0 && color.R < 80 && color.G is >= 70 and <= 130 && color.B > 180);

    private static int CountMutedFillPixels(Bitmap bitmap) =>
        CountPixels(bitmap, color => color.A > 0 && color.R is >= 80 and <= 130 && color.G is >= 95 and <= 135 && color.B is >= 120 and <= 160);

    private static int CountPixels(Bitmap bitmap, Func<Color, bool> predicate)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (predicate(bitmap.GetPixel(x, y)))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
