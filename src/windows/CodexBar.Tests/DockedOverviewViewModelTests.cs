using CodexBar.Core.Models;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.Tests;

[TestClass]
public sealed class DockedOverviewViewModelTests
{
    [TestMethod]
    public void FormatsRowsForSnapshots()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1000);
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Codex,
                "Codex",
                now,
                new[] { new RateWindow("session", "Session", 8, now.AddHours(2), null) },
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

        Assert.AreEqual("Codex", vm.Rows[0].ProviderName);
        Assert.AreEqual("92%", vm.Rows[0].PercentText);
        Assert.AreEqual("Resets in 2h", vm.Rows[0].ResetText);
    }
}
