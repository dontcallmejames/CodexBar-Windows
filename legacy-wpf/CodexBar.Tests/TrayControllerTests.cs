using CodexBar.Core.Models;
using CodexBar.WinApp.Services;

namespace CodexBar.Tests;

[TestClass]
public sealed class TrayControllerTests
{
    [TestMethod]
    public void MostConstrainedProvider_DrivesTrayBadge()
    {
        // Two snapshots: Codex at 30%, Claude at 75%.
        // The tray should reflect the higher (most-constrained) UsedPercent.
        var codexWindow = new RateWindow("codex-primary", "Codex Primary", UsedPercent: 30.0, ResetsAt: null, WindowMinutes: null);
        var claudeWindow = new RateWindow("claude-primary", "Claude Primary", UsedPercent: 75.0, ResetsAt: null, WindowMinutes: null);

        var codexSnapshot = new UsageSnapshot(
            UsageProvider.Codex, "Codex", DateTimeOffset.Now,
            new[] { codexWindow }, null, null, null, null, null, null, null, "api", null, false);
        var claudeSnapshot = new UsageSnapshot(
            UsageProvider.Claude, "Claude", DateTimeOffset.Now,
            new[] { claudeWindow }, null, null, null, null, null, null, null, "api", null, false);

        var model = TrayController.SelectDisplay(new[] { codexSnapshot, claudeSnapshot }, showUsageAsUsed: true);

        Assert.AreEqual(75.0, model.Percent);
        Assert.AreEqual("CodexBar", model.Tooltip);
        Assert.IsFalse(model.IsStale);
    }

    [TestMethod]
    public void NoUsableSnapshots_ReturnsDefaultModel()
    {
        var model = TrayController.SelectDisplay(Array.Empty<UsageSnapshot>(), showUsageAsUsed: true);

        Assert.AreEqual("CodexBar", model.Tooltip);
        Assert.AreEqual(0.0, model.Percent);
        Assert.IsTrue(model.IsStale);
    }

    [TestMethod]
    public void StaleSnapshot_SetsIsStale()
    {
        var window = new RateWindow("w1", "Window 1", UsedPercent: 50.0, ResetsAt: null, WindowMinutes: null);
        var staleSnapshot = new UsageSnapshot(
            UsageProvider.Codex, "Codex", DateTimeOffset.Now,
            new[] { window }, null, null, null, null, null, null, null, "api", null, IsStale: true);

        var model = TrayController.SelectDisplay(new[] { staleSnapshot }, showUsageAsUsed: true);

        Assert.IsTrue(model.IsStale);
        Assert.AreEqual(50.0, model.Percent);
    }

    [TestMethod]
    public void SnapshotWithNoWindows_YieldsZeroPercent()
    {
        var snapshot = UsageSnapshot.MissingCredentials(UsageProvider.Codex, "Codex", "No credentials configured.");

        var model = TrayController.SelectDisplay(new[] { snapshot }, showUsageAsUsed: true);

        Assert.AreEqual("CodexBar", model.Tooltip);
        Assert.AreEqual(0.0, model.Percent);
        Assert.IsTrue(model.IsStale);
    }
}
