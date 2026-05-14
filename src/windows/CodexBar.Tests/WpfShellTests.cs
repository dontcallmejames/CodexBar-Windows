using System.Globalization;
using System.Windows.Media;
using CodexBar.WinApp.Services;
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
        var position = WindowCoordinator.CalculatePopoverPosition(
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
        var maxHeight = WindowCoordinator.CalculatePopoverMaxHeight(
            workArea: new System.Windows.Rect(0, 0, 1920, 1040),
            cursorPosition: new System.Drawing.Point(1900, 1030));

        Assert.AreEqual(1002, maxHeight);
    }

    [TestMethod]
    public void CalculatesPopoverPositionWhenWindowConsumesAvailableHeight()
    {
        var position = WindowCoordinator.CalculatePopoverPosition(
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

        var shortPosition = WindowCoordinator.CalculatePopoverPosition(
            width: 372,
            height: 420,
            workArea,
            cursorPosition);
        var expandedPosition = WindowCoordinator.CalculatePopoverPosition(
            width: 372,
            height: 780,
            workArea,
            cursorPosition);

        Assert.AreEqual(shortPosition.Top - 360, expandedPosition.Top);
        Assert.AreEqual(1018, expandedPosition.Top + 780);
    }

    [TestMethod]
    public void TrayPopoverCapturesCursorAnchorOnceForResizeRepositioning()
    {
        var lifecycleCodePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Services",
            "WindowCoordinator.Lifecycle.cs"));
        var lifecycleCode = File.ReadAllText(lifecycleCodePath);

        StringAssert.Contains(lifecycleCode, "var cursorPosition = System.Windows.Forms.Cursor.Position;");
        StringAssert.Contains(lifecycleCode, "window => ApplyPopoverPositionNearCursor(window, cursorPosition)");
        Assert.IsFalse(lifecycleCode.Contains("ShowPopoverWindow(UsageProvider.Codex, ApplyPopoverPositionNearCursor)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WiresPeriodicRefreshTimerFromSettings()
    {
        var interval = WindowCoordinator.CalculateRefreshInterval(refreshMinutes: 5);
        var minimum = WindowCoordinator.CalculateRefreshInterval(refreshMinutes: 0);
        var appCodePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "App.xaml.cs"));
        var appCode = File.ReadAllText(appCodePath);

        Assert.AreEqual(TimeSpan.FromMinutes(5), interval);
        Assert.AreEqual(TimeSpan.FromMinutes(1), minimum);
        StringAssert.Contains(appCode, "DispatcherTimer");
        StringAssert.Contains(appCode, "StartRefreshTimer(settings)");
        StringAssert.Contains(appCode, "RefreshTimer_Tick");
    }

    [TestMethod]
    public void WiresPeriodicBackgroundUpdateTimerFromSettings()
    {
        var enabledInterval = WindowCoordinator.CalculateUpdateCheckInterval(checkForUpdatesAutomatically: true);
        var disabledInterval = WindowCoordinator.CalculateUpdateCheckInterval(checkForUpdatesAutomatically: false);
        var appCodePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "App.xaml.cs"));
        var appCode = File.ReadAllText(appCodePath);

        Assert.AreEqual(TimeSpan.FromHours(24), enabledInterval);
        Assert.IsNull(disabledInterval);
        StringAssert.Contains(appCode, "StartUpdateCheckTimer(settings)");
        StringAssert.Contains(appCode, "UpdateCheckTimer_Tick");
        StringAssert.Contains(appCode, "CheckForUpdatesInBackgroundAsync");
        StringAssert.Contains(appCode, "ShowUpdateAvailableNotification");
    }

    [TestMethod]
    public void WindowEventHandlersAreUnwiredOnClose()
    {
        var lifecycleCodePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Services",
            "WindowCoordinator.Lifecycle.cs"));
        var lifecycleCode = File.ReadAllText(lifecycleCodePath);

        StringAssert.Contains(lifecycleCode, "UnwirePopoverWindowEvents");
        StringAssert.Contains(lifecycleCode, "UnwireSettingsWindowEvents");
        StringAssert.Contains(lifecycleCode, "window.SizeChanged -= Popover_SizeChanged");
        StringAssert.Contains(lifecycleCode, "window.SettingsSaved -= SettingsWindow_SettingsSaved");
    }

    [TestMethod]
    public void CalculatesSettingsPositionNextToPopoverWhenSpaceAllows()
    {
        var position = CodexBar.WinApp.App.CalculateSettingsPosition(
            settingsWidth: 560,
            settingsHeight: 620,
            anchorLeft: 1400,
            anchorTop: 400,
            anchorWidth: 372,
            anchorHeight: 500,
            workArea: new System.Windows.Rect(0, 0, 2560, 1040));

        Assert.AreEqual(1784, position.Left);
        Assert.AreEqual(340, position.Top);
    }

    [TestMethod]
    public void CalculatesSettingsPositionToLeftWhenRightSideWouldOverflow()
    {
        var position = CodexBar.WinApp.App.CalculateSettingsPosition(
            settingsWidth: 560,
            settingsHeight: 620,
            anchorLeft: 2120,
            anchorTop: 400,
            anchorWidth: 372,
            anchorHeight: 500,
            workArea: new System.Windows.Rect(0, 0, 2560, 1040));

        Assert.AreEqual(1548, position.Left);
        Assert.AreEqual(340, position.Top);
    }

    [TestMethod]
    public void CalculatesTaskbarDockPositionNearBottomRight()
    {
        var position = WindowCoordinator.CalculateTaskbarDockPosition(
            width: 320,
            height: 64,
            workArea: new System.Windows.Rect(0, 0, 2560, 1040));

        Assert.AreEqual(2224, position.Left);
        Assert.AreEqual(964, position.Top);
    }

    [TestMethod]
    public void CalculatesTaskbarDockPositionWithMinimumMarginsWhenWorkAreaIsTooSmall()
    {
        var position = WindowCoordinator.CalculateTaskbarDockPosition(
            width: 600,
            height: 220,
            workArea: new System.Windows.Rect(100, 50, 500, 180));

        Assert.AreEqual(116, position.Left);
        Assert.AreEqual(66, position.Top);
    }

    [TestMethod]
    public void CalculatesPopoverPositionAboveTaskbarDock()
    {
        var position = WindowCoordinator.CalculatePopoverPositionNearDock(
            width: 372,
            height: 500,
            workArea: new System.Windows.Rect(0, 0, 2560, 1040),
            dockLeft: 2224,
            dockTop: 964,
            dockWidth: 320);

        Assert.AreEqual(2172, position.Left);
        Assert.AreEqual(452, position.Top);
    }

    [TestMethod]
    public void CalculatesPopoverMaxHeightFromTaskbarDockAnchor()
    {
        var maxHeight = WindowCoordinator.CalculatePopoverMaxHeightNearDock(
            workArea: new System.Windows.Rect(0, 0, 2560, 1040),
            dockTop: 964);

        Assert.AreEqual(936, maxHeight);
    }

    [TestMethod]
    public void AboutWindowUsesManualPlacementAndCompactSize()
    {
        var aboutXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "AboutWindow.xaml"));

        var aboutXaml = File.ReadAllText(aboutXamlPath);

        StringAssert.Contains(aboutXaml, "WindowStartupLocation=\"Manual\"");
        StringAssert.Contains(aboutXaml, "Width=\"320\"");
        StringAssert.Contains(aboutXaml, "Height=\"230\"");
    }

    [TestMethod]
    public void TaskbarDockWindowUsesCompactTranslucentSurface()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "TaskbarDockWindow.xaml"));

        var xaml = File.ReadAllText(xamlPath);

        StringAssert.Contains(xaml, "ShowInTaskbar=\"False\"");
        StringAssert.Contains(xaml, "Topmost=\"True\"");
        StringAssert.Contains(xaml, "Width=\"320\"");
        StringAssert.Contains(xaml, "{StaticResource CodexBarPanelBrush}");
        StringAssert.Contains(xaml, "Hide Taskbar Dock");
    }
}
