namespace CodexBar.WinApp.Services;

public sealed partial class WindowCoordinator
{
    internal static double WindowWidth(System.Windows.Window window) =>
        window.ActualWidth > 0 ? window.ActualWidth : window.Width;

    internal static double WindowHeight(System.Windows.Window window)
    {
        if (window.ActualHeight > 0)
        {
            return window.ActualHeight;
        }

        return double.IsNaN(window.Height) ? window.MaxHeight : window.Height;
    }

    internal static void PositionPopoverNearCursor(System.Windows.Window window, System.Drawing.Point cursorPosition)
    {
        window.MaxHeight = CalculatePopoverMaxHeight(System.Windows.SystemParameters.WorkArea, cursorPosition);
        var width = WindowWidth(window);
        var height = WindowHeight(window);
        var position = CalculatePopoverPosition(width, height, System.Windows.SystemParameters.WorkArea, cursorPosition);
        window.Left = position.Left;
        window.Top = position.Top;
    }

    public static (double Left, double Top) CalculatePopoverPosition(
        double width,
        double height,
        System.Windows.Rect workArea,
        System.Drawing.Point cursorPosition)
    {
        const double margin = 16;
        var left = cursorPosition.X - width + 24;
        var bottom = cursorPosition.Y - 12;
        var top = bottom - height;
        var maxLeft = workArea.Right - width - margin;
        var maxTop = Math.Max(workArea.Top + margin, workArea.Bottom - height - margin);

        return (
            Math.Clamp(left, workArea.Left + margin, maxLeft),
            Math.Clamp(top, workArea.Top + margin, maxTop));
    }

    public static double CalculatePopoverMaxHeight(
        System.Windows.Rect workArea,
        System.Drawing.Point cursorPosition)
    {
        const double margin = 16;
        const double trayGap = 12;
        const double minimumHeight = 360;
        var anchoredBottom = Math.Clamp(cursorPosition.Y - trayGap, workArea.Top + margin + minimumHeight, workArea.Bottom - margin);
        return Math.Max(minimumHeight, anchoredBottom - workArea.Top - margin);
    }

    public static double CalculatePopoverMaxHeightNearDock(
        System.Windows.Rect workArea,
        double dockTop)
    {
        const double margin = 16;
        const double dockGap = 12;
        const double minimumHeight = 360;
        return Math.Max(minimumHeight, dockTop - dockGap - workArea.Top - margin);
    }

    public static (double Left, double Top) CalculatePopoverPositionNearDock(
        double width,
        double height,
        System.Windows.Rect workArea,
        double dockLeft,
        double dockTop,
        double dockWidth)
    {
        const double margin = 16;
        const double dockGap = 12;
        var minLeft = workArea.Left + margin;
        var maxLeft = workArea.Right - width - margin;
        var minTop = workArea.Top + margin;
        var maxTop = dockTop - height - dockGap;
        var left = dockLeft + dockWidth - width;

        return (
            maxLeft < minLeft ? minLeft : Math.Clamp(left, minLeft, maxLeft),
            maxTop < minTop ? minTop : Math.Clamp(maxTop, minTop, maxTop));
    }

    public static (double Left, double Top) CalculateTaskbarDockPosition(
        double width,
        double height,
        System.Windows.Rect workArea)
    {
        const double margin = 16;
        const double taskbarGap = 12;
        var minLeft = workArea.Left + margin;
        var maxLeft = workArea.Right - width - margin;
        var minTop = workArea.Top + margin;
        var maxTop = workArea.Bottom - height - taskbarGap;

        return (
            maxLeft < minLeft ? minLeft : Math.Clamp(maxLeft, minLeft, maxLeft),
            maxTop < minTop ? minTop : Math.Clamp(maxTop, minTop, maxTop));
    }

    public static TimeSpan CalculateRefreshInterval(int refreshMinutes) =>
        TimeSpan.FromMinutes(Math.Max(1, refreshMinutes));

    public static TimeSpan? CalculateUpdateCheckInterval(bool checkForUpdatesAutomatically) =>
        checkForUpdatesAutomatically ? TimeSpan.FromHours(24) : null;

    /// <summary>
    /// Calculates the screen position for the settings/first-run window relative to an anchor.
    /// </summary>
    public static (double Left, double Top) CalculateSettingsPosition(
        double settingsWidth,
        double settingsHeight,
        double anchorLeft,
        double anchorTop,
        double anchorWidth,
        double anchorHeight,
        System.Windows.Rect workArea)
    {
        const double margin = 16;
        const double gap = 12;
        var rightCandidate = anchorLeft + anchorWidth + gap;
        var leftCandidate = anchorLeft - settingsWidth - gap;
        var maxLeft = workArea.Right - settingsWidth - margin;
        var left = rightCandidate <= maxLeft ? rightCandidate : leftCandidate;
        var anchorCenter = anchorTop + (anchorHeight / 2);
        var top = anchorCenter - (settingsHeight / 2);
        var maxTop = workArea.Bottom - settingsHeight - margin;

        return (
            Math.Clamp(left, workArea.Left + margin, maxLeft),
            Math.Clamp(top, workArea.Top + margin, maxTop));
    }
}
