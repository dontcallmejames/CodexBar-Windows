using System.Text.Json;
using CodexBar.Core.Providers.Codex;

namespace CodexBar.Tests;

[TestClass]
public sealed class CodexOAuthUsageMapperTests
{
    [TestMethod]
    public void MapsLiveRateLimitPayloadAndStringCredits()
    {
        const string json = """
        {
          "rate_limit": {
            "primary_window": { "used_percent": 12.5, "reset_at": 1893456000, "limit_window_seconds": 18000 },
            "secondary_window": { "used_percent": 35.0, "reset_at": 1893715200, "limit_window_seconds": 604800 }
          },
          "credits": { "balance": "42.75", "has_credits": true },
          "plan_type": "Pro"
        }
        """;

        var response = JsonSerializer.Deserialize<CodexOAuthUsageResponse>(json, CodexOAuthUsageMapper.JsonOptions)!;
        var snapshot = CodexOAuthUsageMapper.Map(response, DateTimeOffset.FromUnixTimeSeconds(1893440000));

        Assert.AreEqual("Codex", snapshot.DisplayName);
        Assert.AreEqual(2, snapshot.Windows.Count);
        Assert.AreEqual("5-hour", snapshot.Windows[0].Title);
        Assert.AreEqual(12.5, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1893456000), snapshot.Windows[0].ResetsAt);
        Assert.AreEqual("Weekly", snapshot.Windows[1].Title);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1893715200), snapshot.Windows[1].ResetsAt);
        Assert.AreEqual(42.75m, snapshot.CreditsRemaining);
        Assert.IsNull(snapshot.AccountEmail);
        Assert.AreEqual("Pro", snapshot.Plan);
        Assert.AreEqual("oauth", snapshot.SourceLabel);
    }
}
