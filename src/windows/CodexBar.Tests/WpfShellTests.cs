using System.Globalization;
using System.Windows.Media;
using CodexBar.WinApp.Views;
using MediaColor = System.Windows.Media.Color;

namespace CodexBar.Tests;

[TestClass]
public sealed class WpfShellTests
{
    [TestMethod]
    public void BooleanToBrushConverterUsesAccentForActiveState()
    {
        var active = (SolidColorBrush)BooleanToBrushConverter.Instance.Convert(
            true,
            typeof(SolidColorBrush),
            null,
            CultureInfo.InvariantCulture);
        var inactive = (SolidColorBrush)BooleanToBrushConverter.Instance.Convert(
            false,
            typeof(SolidColorBrush),
            null,
            CultureInfo.InvariantCulture);

        Assert.AreEqual(MediaColor.FromRgb(47, 123, 246), active.Color);
        Assert.AreEqual(MediaColor.FromArgb(45, 255, 255, 255), inactive.Color);
    }
}
