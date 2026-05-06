using CodexBar.Core.Models;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.Tests;

[TestClass]
public sealed class PopoverViewModelTests
{
    [TestMethod]
    public void BuildsTabsAndActiveSnapshot()
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
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

        var vm = new PopoverViewModel(snapshots, UsageProvider.Claude, showUsageAsUsed: true, now: now);

        Assert.AreEqual("Claude", vm.ActiveSnapshot!.DisplayName);
        Assert.AreEqual(2, vm.Tabs.Count);
        Assert.AreEqual("30%", vm.Tabs[1].PercentText);
        Assert.AreEqual(30, vm.Tabs[1].ProgressPercent);
        Assert.IsFalse(string.IsNullOrWhiteSpace(vm.Tabs[1].IconGeometry));
        Assert.IsTrue(vm.Tabs[1].IsStale);
    }

    [TestMethod]
    public void BuildsOriginalMenuCardRows()
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Claude,
                "Claude",
                now,
                new[]
                {
                    new RateWindow("session", "Session", 2, now.AddHours(3).AddMinutes(53), null),
                    new RateWindow("weekly", "Weekly", 3, now.AddDays(3).AddHours(20), null),
                    new RateWindow("sonnet", "Sonnet", 0, null, null)
                },
                null,
                "Max",
                null,
                0.04m,
                15000,
                254.24m,
                218000000,
                "test",
                null,
                false)
        };

        var vm = new PopoverViewModel(snapshots, UsageProvider.Claude, showUsageAsUsed: true, now: now);

        Assert.AreEqual("Updated just now", vm.UpdatedText);
        Assert.AreEqual("Max", vm.PlanText);
        Assert.AreEqual(3, vm.Metrics.Count);
        Assert.AreEqual("2% used", vm.Metrics[0].PercentText);
        Assert.AreEqual("Resets in 3h 53m", vm.Metrics[0].ResetText);
        Assert.AreEqual("Today: $0.04 \u00b7 15K tokens", vm.CostTodayText);
        Assert.AreEqual("Last 30 days: $254.24 \u00b7 218M tokens", vm.CostLast30DaysText);
        CollectionAssert.AreEqual(
            new[] { "Add Account...", "Usage Dashboard", "Status Page", "Settings...", "About CodexBar", "Quit" },
            vm.FooterRows.Select(row => row.Title).ToArray());
    }

    [TestMethod]
    public void BuildsPlaceholderMetricWhenActiveSnapshotHasNoWindows()
    {
        var snapshots = new[]
        {
            UsageSnapshot.MissingCredentials(
                UsageProvider.Claude,
                "Claude",
                "Claude OAuth token expired. Run claude login, then retry.")
        };

        var vm = new PopoverViewModel(snapshots, UsageProvider.Claude, showUsageAsUsed: true);

        Assert.AreEqual(1, vm.Metrics.Count);
        Assert.AreEqual("No usage data", vm.Metrics[0].Title);
        Assert.AreEqual("Claude OAuth token expired. Run claude login, then retry.", vm.Metrics[0].PercentText);
        Assert.AreEqual(0, vm.Metrics[0].ProgressPercent);
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
    public void SelectProviderCommandUpdatesActiveProviderAndTabs()
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
                false),
            new UsageSnapshot(
                UsageProvider.Claude,
                "Claude",
                now,
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
                false)
        };

        var vm = new PopoverViewModel(snapshots, UsageProvider.Codex, showUsageAsUsed: true);

        vm.SelectProviderCommand.Execute(UsageProvider.Claude);

        Assert.AreEqual(UsageProvider.Claude, vm.ActiveProvider);
        Assert.AreEqual("Claude", vm.ActiveSnapshot!.DisplayName);
        Assert.IsFalse(vm.Tabs.Single(tab => tab.Provider == UsageProvider.Codex).IsActive);
        Assert.IsTrue(vm.Tabs.Single(tab => tab.Provider == UsageProvider.Claude).IsActive);
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
