using CodexBar.Tray;

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
}
