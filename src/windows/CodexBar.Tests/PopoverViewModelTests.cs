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
    public void SelectsFallbackTabWhenActiveProviderIsMissing()
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
                false)
        };

        var vm = new PopoverViewModel(snapshots, UsageProvider.Claude, showUsageAsUsed: true);

        Assert.AreEqual(UsageProvider.Codex, vm.ActiveProvider);
        Assert.AreEqual("Codex", vm.ActiveSnapshot!.DisplayName);
        Assert.IsTrue(vm.Tabs.Single().IsActive);
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
        Assert.AreEqual($"Updated {now:t}", vm.UpdatedText);
        Assert.AreEqual("Codex", vm.Rows[0].ProviderName);
        Assert.AreEqual("80%", vm.Rows[0].PercentText);
        Assert.AreEqual("Resets in 45m", vm.Rows[0].ResetText);
        Assert.IsFalse(vm.Rows[0].IsStale);
    }

    [TestMethod]
    public void FloorsRelativeResetTextAtHourAndDayBoundaries()
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            SnapshotWithReset(now.AddMinutes(89)),
            SnapshotWithReset(now.AddHours(23.6))
        };

        var vm = new DockedOverviewViewModel(snapshots, showUsageAsUsed: false, now);

        Assert.AreEqual("Resets in 1h", vm.Rows[0].ResetText);
        Assert.AreEqual("Resets in 23h", vm.Rows[1].ResetText);
    }

    [TestMethod]
    public void RunsPopoverFooterCommands()
    {
        var invocations = new List<string>();
        var vm = new PopoverViewModel(
            Array.Empty<UsageSnapshot>(),
            UsageProvider.Codex,
            showUsageAsUsed: true,
            openDashboard: () => invocations.Add("dashboard"),
            openSettings: () => invocations.Add("settings"),
            showAbout: () => invocations.Add("about"),
            quit: () => invocations.Add("quit"));

        vm.UsageDashboardCommand.Execute(null);
        vm.SettingsCommand.Execute(null);
        vm.AboutCommand.Execute(null);
        vm.QuitCommand.Execute(null);

        CollectionAssert.AreEqual(
            new[] { "dashboard", "settings", "about", "quit" },
            invocations);
    }

    private static UsageSnapshot SnapshotWithReset(DateTimeOffset resetsAt) =>
        new(
            UsageProvider.Codex,
            "Codex",
            DateTimeOffset.Now,
            new[] { new RateWindow("session", "Session", 20, resetsAt, null) },
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
}
