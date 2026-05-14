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
}
