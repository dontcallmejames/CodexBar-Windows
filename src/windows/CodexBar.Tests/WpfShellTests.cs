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

    [TestMethod]
    public void CalculatesPopoverPositionNearBottomRightTray()
    {
        var position = CodexBar.WinApp.App.CalculatePopoverPosition(
            width: 430,
            height: 360,
            workArea: new System.Windows.Rect(0, 0, 1920, 1040),
            cursorPosition: new System.Drawing.Point(1900, 1030));

        Assert.IsTrue(position.Left > 1400);
        Assert.IsTrue(position.Top > 600);
        Assert.IsTrue(position.Left <= 1474);
        Assert.IsTrue(position.Top <= 664);
    }
}
