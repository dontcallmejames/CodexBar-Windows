using CodexBar.Core.Models;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.Tests;

[TestClass]
public sealed class TaskbarDockViewModelTests
{
    [TestMethod]
    public void BuildsTilesForProviderSnapshots()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1000);
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Codex,
                "Codex",
                now,
                new[] { new RateWindow("session", "Session", 34, now.AddHours(2), null) },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "test",
                null,
                false),
            new UsageSnapshot(
                UsageProvider.Claude,
                "Claude",
                now,
                new[] { new RateWindow("sonnet", "Sonnet", 1, now.AddDays(1), null) },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "test",
                null,
                true)
        };

        var vm = new TaskbarDockViewModel(snapshots, showUsageAsUsed: true);

        Assert.AreEqual(2, vm.Tiles.Count);
        Assert.AreEqual("Codex", vm.Tiles[0].ProviderName);
        Assert.AreEqual("34% used", vm.Tiles[0].PercentText);
        Assert.AreEqual(34, vm.Tiles[0].ProgressPercent);
        Assert.AreEqual("#35D2C6", vm.Tiles[0].ProgressColor);
        Assert.IsFalse(vm.Tiles[0].IsStale);
        Assert.IsFalse(vm.Tiles[0].IsEmpty);
        Assert.AreEqual("Claude", vm.Tiles[1].ProviderName);
        Assert.AreEqual("1% used", vm.Tiles[1].PercentText);
        Assert.IsTrue(vm.Tiles[1].IsStale);
    }

    [TestMethod]
    public void ShowsEmptyTileWhenSnapshotHasNoWindows()
    {
        var snapshot = UsageSnapshot.MissingCredentials(
            UsageProvider.Gemini,
            "Gemini",
            "Refreshing usage...");

        var vm = new TaskbarDockViewModel(new[] { snapshot }, showUsageAsUsed: true);

        Assert.AreEqual(1, vm.Tiles.Count);
        Assert.AreEqual("Gemini", vm.Tiles[0].ProviderName);
        Assert.AreEqual("--", vm.Tiles[0].PercentText);
        Assert.AreEqual(0, vm.Tiles[0].ProgressPercent);
        Assert.AreEqual("#B8B2C8", vm.Tiles[0].ProgressColor);
        Assert.IsTrue(vm.Tiles[0].IsEmpty);
        Assert.IsTrue(vm.Tiles[0].IsStale);
    }

    [TestMethod]
    public void RespectsRemainingModeWhenShowUsageAsUsedIsFalse()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1000);
        var snapshot = new UsageSnapshot(
            UsageProvider.Cursor,
            "Cursor",
            now,
            new[] { new RateWindow("included", "Included", 18, now.AddDays(10), null) },
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "test",
            null,
            false);

        var vm = new TaskbarDockViewModel(new[] { snapshot }, showUsageAsUsed: false);

        Assert.AreEqual("82% left", vm.Tiles[0].PercentText);
        Assert.AreEqual(82, vm.Tiles[0].ProgressPercent);
    }
}
