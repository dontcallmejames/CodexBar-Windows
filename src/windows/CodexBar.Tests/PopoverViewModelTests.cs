using CodexBar.Core.Models;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.Tests;

[TestClass]
public sealed class PopoverViewModelTests
{
    [TestMethod]
    public void BuildsTabsAndActiveSnapshot()
    {
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Codex,
                "Codex",
                DateTimeOffset.Now,
                new[] { new RateWindow("session", "Session", 20, null, null) },
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
                DateTimeOffset.Now,
                new[] { new RateWindow("session", "Session", 30, null, null) },
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

        var vm = new PopoverViewModel(snapshots, UsageProvider.Claude, showUsageAsUsed: true);

        Assert.AreEqual("Claude", vm.ActiveSnapshot!.DisplayName);
        Assert.AreEqual(2, vm.Tabs.Count);
        Assert.AreEqual("30%", vm.Tabs[1].PercentText);
        Assert.IsTrue(vm.Tabs[1].IsStale);
    }

    [TestMethod]
    public void BuildsDockedRowsWithRelativeResetText()
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Codex,
                "Codex",
                now,
                new[] { new RateWindow("session", "Session", 20, now.AddMinutes(45), null) },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "test",
                null,
                false)
        };

        var vm = new DockedOverviewViewModel(snapshots, showUsageAsUsed: false, now);

        Assert.AreEqual(1, vm.Rows.Count);
        Assert.AreEqual("Codex", vm.Rows[0].ProviderName);
        Assert.AreEqual("80%", vm.Rows[0].PercentText);
        Assert.AreEqual("Resets in 45m", vm.Rows[0].ResetText);
        Assert.IsFalse(vm.Rows[0].IsStale);
    }
}
