using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Copilot;

namespace CodexBar.Tests;

[TestClass]
public sealed class CopilotUsageMapperTests
{
    [TestMethod]
    public void MapsPaidTierUsingQuotaSnapshots()
    {
        const string json = """
        {
          "copilot_plan": "pro",
          "quota_snapshots": {
            "premium_interactions": { "percent_remaining": 87.5 },
            "chat":                  { "percent_remaining": 92.0 }
          },
          "quota_reset_date": "2026-06-01"
        }
        """;

        var response = JsonSerializer.Deserialize<CopilotUserResponse>(json, CopilotUsageMapper.JsonOptions)!;
        var snapshot = CopilotUsageMapper.Map(response, DateTimeOffset.Parse("2026-05-18T00:00:00Z"));

        Assert.AreEqual(UsageProvider.Copilot, snapshot.Provider);
        Assert.AreEqual("Copilot", snapshot.DisplayName);
        Assert.AreEqual("Pro", snapshot.Plan);
        Assert.AreEqual("gh-cli", snapshot.SourceLabel);
        Assert.AreEqual(2, snapshot.Windows.Count);

        Assert.AreEqual("Premium", snapshot.Windows[0].Title);
        Assert.AreEqual(12.5, snapshot.Windows[0].UsedPercent, 0.001);

        Assert.AreEqual("Chat", snapshot.Windows[1].Title);
        Assert.AreEqual(8.0, snapshot.Windows[1].UsedPercent, 0.001);

        Assert.IsNotNull(snapshot.Windows[0].ResetsAt);
        Assert.AreEqual(2026, snapshot.Windows[0].ResetsAt!.Value.Year);
        Assert.AreEqual(6, snapshot.Windows[0].ResetsAt!.Value.Month);
    }

    [TestMethod]
    public void MapsFreeTierUsingLimitedAndMonthlyQuotas()
    {
        const string json = """
        {
          "copilot_plan": "free",
          "limited_user_quotas":  { "chat": 12, "completions": 800 },
          "monthly_quotas":       { "chat": 50, "completions": 2000 },
          "limited_user_reset_date": "2026-06-01"
        }
        """;

        var response = JsonSerializer.Deserialize<CopilotUserResponse>(json, CopilotUsageMapper.JsonOptions)!;
        var snapshot = CopilotUsageMapper.Map(response, DateTimeOffset.Parse("2026-05-18T00:00:00Z"));

        Assert.AreEqual("Free", snapshot.Plan);
        Assert.AreEqual(2, snapshot.Windows.Count);

        Assert.AreEqual("Chat", snapshot.Windows[0].Title);
        // used = 50 - 12 = 38; 38/50 = 76%
        Assert.AreEqual(76.0, snapshot.Windows[0].UsedPercent, 0.001);

        Assert.AreEqual("Completions", snapshot.Windows[1].Title);
        // used = 2000 - 800 = 1200; 1200/2000 = 60%
        Assert.AreEqual(60.0, snapshot.Windows[1].UsedPercent, 0.001);
    }

    [TestMethod]
    public void EmptyResponseProducesNoWindowsAndNoCrash()
    {
        var response = new CopilotUserResponse(null, null, null, null, null, null);
        var snapshot = CopilotUsageMapper.Map(response, DateTimeOffset.UtcNow);

        Assert.AreEqual(0, snapshot.Windows.Count);
        Assert.IsNull(snapshot.Plan);
    }
}
