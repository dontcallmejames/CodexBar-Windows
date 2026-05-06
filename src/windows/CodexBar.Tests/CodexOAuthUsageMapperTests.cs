using System.Text.Json;
using CodexBar.Core.Providers.Codex;

namespace CodexBar.Tests;

[TestClass]
public sealed class CodexOAuthUsageMapperTests
{
    [TestMethod]
    public void MapsPrimarySecondaryAndCredits()
    {
        const string json = """
        {
          "primary_window": { "used_percent": 12.5, "resets_at": 1893456000, "limit_window_seconds": 18000 },
          "secondary_window": { "used_percent": 35.0, "resets_at": 1893715200, "limit_window_seconds": 604800 },
          "credits": { "balance": 42.75, "has_credits": true },
          "account": { "email": "dev@example.com", "plan_type": "Pro" }
        }
        """;

        var response = JsonSerializer.Deserialize<CodexOAuthUsageResponse>(json, CodexOAuthUsageMapper.JsonOptions)!;
        var snapshot = CodexOAuthUsageMapper.Map(response, DateTimeOffset.FromUnixTimeSeconds(1893440000));

        Assert.AreEqual("Codex", snapshot.DisplayName);
        Assert.AreEqual(2, snapshot.Windows.Count);
        Assert.AreEqual("5-hour", snapshot.Windows[0].Title);
        Assert.AreEqual(12.5, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual("Weekly", snapshot.Windows[1].Title);
        Assert.AreEqual(42.75m, snapshot.CreditsRemaining);
        Assert.AreEqual("dev@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Pro", snapshot.Plan);
        Assert.AreEqual("oauth", snapshot.SourceLabel);
    }
}
