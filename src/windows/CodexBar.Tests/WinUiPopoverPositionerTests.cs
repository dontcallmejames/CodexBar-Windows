using CodexBar.WinUI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class WinUiPopoverPositionerTests
{
    private const int W = 360, H = 480;
    private const int Wa_X = 0, Wa_Y = 0, Wa_W = 1920, Wa_H = 1080;

    // WPF anchor: left = cursorX - width + 24, top = cursorY - 12 - height
    // margin = 16

    [TestMethod]
    public void Cursor_NearCenter_PopoverAnchoredCorrectly()
    {
        var (left, top) = PopoverPositioner.CalculateForCursor(960, 540, W, H, Wa_X, Wa_Y, Wa_W, Wa_H);
        Assert.AreEqual(960 - W + 24, left, "left = cursorX - width + 24");
        Assert.AreEqual(540 - 12 - H, top, "top = cursorY - gap(12) - height");
    }

    [TestMethod]
    public void Cursor_NearTopEdge_PopoverClampedToWorkAreaTop()
    {
        var (_, top) = PopoverPositioner.CalculateForCursor(960, 5, W, H, Wa_X, Wa_Y, Wa_W, Wa_H);
        Assert.IsTrue(top >= Wa_Y + 16, $"top {top} should be at least workArea.Top + margin(16)");
    }

    [TestMethod]
    public void Cursor_NearRightEdge_PopoverClampedToWorkAreaRight()
    {
        var (left, _) = PopoverPositioner.CalculateForCursor(Wa_W - 10, 540, W, H, Wa_X, Wa_Y, Wa_W, Wa_H);
        Assert.IsTrue(left + W <= Wa_W - 16, $"popover right edge {left + W} should be inside work area (max {Wa_W - 16})");
    }

    [TestMethod]
    public void Cursor_NearLeftEdge_PopoverClampedToWorkAreaLeft()
    {
        var (left, _) = PopoverPositioner.CalculateForCursor(2, 540, W, H, Wa_X, Wa_Y, Wa_W, Wa_H);
        Assert.IsTrue(left >= Wa_X + 16, $"left {left} should be at least workArea.Left + margin(16)");
    }

    [TestMethod]
    public void WorkAreaOffsetByTaskbar_ReturnsPositionInsideOffsetArea()
    {
        // Work area starts at y=40 (taskbar at top), 1000 tall.
        var (_, top) = PopoverPositioner.CalculateForCursor(960, 200, W, H, 0, 40, 1920, 1000);
        Assert.IsTrue(top >= 40 + 16, $"top {top} should respect work-area offset (min {40 + 16})");
    }
}
