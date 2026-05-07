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
        Assert.AreEqual(MediaColor.FromArgb(0, 255, 255, 255), inactive.Color);
    }

    [TestMethod]
    public void PopoverPanelBrushUsesMilkyTranslucentWhite()
    {
        var appXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "App.xaml"));
        var appXaml = File.ReadAllText(appXamlPath);

        StringAssert.Contains(appXaml, "Color=\"#EEFFFFFF\"");
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

    [TestMethod]
    public void CalculatesPopoverMaxHeightFromTrayAnchor()
    {
        var maxHeight = CodexBar.WinApp.App.CalculatePopoverMaxHeight(
            workArea: new System.Windows.Rect(0, 0, 1920, 1040),
            cursorPosition: new System.Drawing.Point(1900, 1030));

        Assert.AreEqual(1002, maxHeight);
    }

    [TestMethod]
    public void CalculatesPopoverPositionWhenWindowConsumesAvailableHeight()
    {
        var position = CodexBar.WinApp.App.CalculatePopoverPosition(
            width: 372,
            height: 1002,
            workArea: new System.Windows.Rect(0, 0, 1920, 1040),
            cursorPosition: new System.Drawing.Point(1900, 1030));

        Assert.AreEqual(16, position.Top);
        Assert.IsTrue(position.Left > 1500);
    }

    [TestMethod]
    public void RepositionsExpandedPopoverAboveSameTrayAnchor()
    {
        var workArea = new System.Windows.Rect(0, 0, 1920, 1040);
        var cursorPosition = new System.Drawing.Point(1900, 1030);

        var shortPosition = CodexBar.WinApp.App.CalculatePopoverPosition(
            width: 372,
            height: 420,
            workArea,
            cursorPosition);
        var expandedPosition = CodexBar.WinApp.App.CalculatePopoverPosition(
            width: 372,
            height: 780,
            workArea,
            cursorPosition);

        Assert.AreEqual(shortPosition.Top - 360, expandedPosition.Top);
        Assert.AreEqual(1018, expandedPosition.Top + 780);
    }
}
