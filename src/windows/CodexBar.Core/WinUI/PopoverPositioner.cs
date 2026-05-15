namespace CodexBar.WinUI.Services;

public static class PopoverPositioner
{
    private const int Margin = 16;
    private const int TrayGap = 12;

    /// <summary>
    /// Pixel position for a popover anchored near a cursor click, clamped inside the work area.
    /// Mirrors WPF WindowCoordinator.CalculatePopoverPosition but expressed in raw ints
    /// to keep CodexBar.Core free of Windows App SDK types.
    /// Anchor: left edge is cursorX - width + 24 (popover sits left of cursor with 24px inset);
    /// bottom edge is cursorY - 12 (12px gap above the cursor/tray icon).
    /// </summary>
    public static (int Left, int Top) CalculateForCursor(
        int cursorX,
        int cursorY,
        int popoverWidth,
        int popoverHeight,
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight)
    {
        var left = cursorX - popoverWidth + 24;
        var bottom = cursorY - TrayGap;
        var top = bottom - popoverHeight;

        var maxLeft = workAreaX + workAreaWidth - popoverWidth - Margin;
        var maxTop = System.Math.Max(workAreaY + Margin, workAreaY + workAreaHeight - popoverHeight - Margin);

        left = System.Math.Clamp(left, workAreaX + Margin, maxLeft);
        top = System.Math.Clamp(top, workAreaY + Margin, maxTop);

        return (left, top);
    }

    /// <summary>
    /// Pixel position for a secondary window (Settings, About) anchored next to an anchor rect
    /// (usually the popover when it's open, else the cursor). Prefers right side; falls back to
    /// left side if right would clip. Vertically centered on the anchor. Clamped to work area.
    /// Mirrors WPF WindowCoordinator.CalculateSettingsPosition.
    /// </summary>
    public static (int Left, int Top) CalculateNearAnchor(
        int windowWidth,
        int windowHeight,
        int anchorLeft,
        int anchorTop,
        int anchorWidth,
        int anchorHeight,
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight)
    {
        const int gap = 12;
        var rightCandidate = anchorLeft + anchorWidth + gap;
        var leftCandidate = anchorLeft - windowWidth - gap;
        var maxLeft = workAreaX + workAreaWidth - windowWidth - Margin;
        var left = rightCandidate <= maxLeft ? rightCandidate : leftCandidate;

        var anchorCenter = anchorTop + (anchorHeight / 2);
        var top = anchorCenter - (windowHeight / 2);
        var minLeft = workAreaX + Margin;
        var minTop = workAreaY + Margin;
        var maxTop = workAreaY + workAreaHeight - windowHeight - Margin;

        return (
            System.Math.Clamp(left, minLeft, System.Math.Max(minLeft, maxLeft)),
            System.Math.Clamp(top, minTop, System.Math.Max(minTop, maxTop)));
    }

    /// <summary>
    /// Pixel position for the taskbar dock strip, anchored bottom-right of the work area
    /// with a small gap above the taskbar. Mirrors WPF WindowCoordinator.CalculateTaskbarDockPosition.
    /// </summary>
    public static (int Left, int Top) CalculateTaskbarDock(
        int width,
        int height,
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight)
    {
        const int taskbarGap = 12;
        var minLeft = workAreaX + Margin;
        var maxLeft = workAreaX + workAreaWidth - width - Margin;
        var minTop = workAreaY + Margin;
        var maxTop = workAreaY + workAreaHeight - height - taskbarGap;

        var left = maxLeft < minLeft ? minLeft : System.Math.Clamp(maxLeft, minLeft, maxLeft);
        var top = maxTop < minTop ? minTop : System.Math.Clamp(maxTop, minTop, maxTop);

        return (left, top);
    }
}
