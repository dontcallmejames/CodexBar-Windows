using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Claude;

namespace CodexBar.Tests;

[TestClass]
public sealed class ClaudeUsageMapperTests
{
    [TestMethod]
    public void MapsSessionWeeklySonnetExtraUsageAndAccount()
    {
        const string json = """
        {
          "five_hour": { "utilization": 12.5, "resets_at": "2030-01-01T00:00:00Z" },
          "seven_day": { "utilization": 35, "resets_at": "2030-01-08T00:00:00Z" },
          "seven_day_sonnet": { "utilization": 7, "resets_at": "2030-01-08T00:00:00Z" },
          "extra_usage": {
            "is_enabled": true,
            "used_usd": 12.34,
            "limit_usd": 50.00,
            "currency": "USD"
          },
          "account": {
            "email": "claude@example.com",
            "subscription_type": "Max"
          }
        }
        """;

        var response = JsonSerializer.Deserialize<ClaudeUsageResponse>(json, ClaudeUsageMapper.JsonOptions)!;
        var snapshot = ClaudeUsageMapper.Map(response, DateTimeOffset.FromUnixTimeSeconds(1893440000), "oauth");

        Assert.AreEqual(UsageProvider.Claude, snapshot.Provider);
        Assert.AreEqual("Claude", snapshot.DisplayName);
        Assert.AreEqual(3, snapshot.Windows.Count);
        Assert.AreEqual("session", snapshot.Windows[0].Id);
        Assert.AreEqual("Session", snapshot.Windows[0].Title);
        Assert.AreEqual(12.5, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual(DateTimeOffset.Parse("2030-01-01T00:00:00Z"), snapshot.Windows[0].ResetsAt);
        Assert.AreEqual(300, snapshot.Windows[0].WindowMinutes);
        Assert.AreEqual("weekly", snapshot.Windows[1].Id);
        Assert.AreEqual("Weekly", snapshot.Windows[1].Title);
        Assert.AreEqual(35, snapshot.Windows[1].UsedPercent);
        Assert.AreEqual(10_080, snapshot.Windows[1].WindowMinutes);
        Assert.AreEqual("sonnet", snapshot.Windows[2].Id);
        Assert.AreEqual("Sonnet", snapshot.Windows[2].Title);
        Assert.AreEqual(7, snapshot.Windows[2].UsedPercent);
        Assert.AreEqual(10_080, snapshot.Windows[2].WindowMinutes);
        Assert.AreEqual("claude@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Max", snapshot.Plan);
        Assert.AreEqual(12.34m, snapshot.TodayCostUsd);
        Assert.AreEqual("oauth", snapshot.SourceLabel);
        Assert.IsFalse(snapshot.IsStale);
    }

    [TestMethod]
    public void UsesWeeklyAsPrimaryWhenSessionIsMissing()
    {
        const string json = """
        {
          "seven_day": { "utilization": 35, "resets_at": "2030-01-08T00:00:00Z" }
        }
        """;

        var response = JsonSerializer.Deserialize<ClaudeUsageResponse>(json, ClaudeUsageMapper.JsonOptions)!;
        var snapshot = ClaudeUsageMapper.Map(response, DateTimeOffset.FromUnixTimeSeconds(1893440000), "web");

        Assert.AreEqual(1, snapshot.Windows.Count);
        Assert.AreEqual("weekly", snapshot.Windows[0].Id);
        Assert.AreEqual("Weekly", snapshot.Windows[0].Title);
        Assert.AreEqual(35, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual("web", snapshot.SourceLabel);
    }

    [TestMethod]
    public void MapsOAuthAppsAndProductUsageAliases()
    {
        const string json = """
        {
          "seven_day_oauth_apps": { "utilization": 22, "resets_at": "2030-01-08T00:00:00Z" },
          "seven_day_omelette": { "utilization": 29, "resets_at": "2030-01-08T00:00:00Z" },
          "seven_day_cowork": { "utilization": 9, "resets_at": "2030-01-08T00:00:00Z" }
        }
        """;

        var response = JsonSerializer.Deserialize<ClaudeUsageResponse>(json, ClaudeUsageMapper.JsonOptions)!;
        var snapshot = ClaudeUsageMapper.Map(response, DateTimeOffset.FromUnixTimeSeconds(1893440000), "oauth");

        CollectionAssert.AreEqual(
            new[] { "Weekly", "Designs", "Daily Routines" },
            snapshot.Windows.Select(window => window.Title).ToArray());
        Assert.AreEqual(22, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual(29, snapshot.Windows[1].UsedPercent);
        Assert.AreEqual(9, snapshot.Windows[2].UsedPercent);
    }

    [TestMethod]
    public void KeepsKnownProductWindowVisibleWhenAliasIsNull()
    {
        const string json = """
        {
          "five_hour": { "utilization": 12.5 },
          "seven_day_cowork": null
        }
        """;

        var response = JsonSerializer.Deserialize<ClaudeUsageResponse>(json, ClaudeUsageMapper.JsonOptions)!;
        var snapshot = ClaudeUsageMapper.Map(response, DateTimeOffset.FromUnixTimeSeconds(1893440000), "oauth");

        var routines = snapshot.Windows.Single(window => window.Id == "routines");
        Assert.AreEqual("Daily Routines", routines.Title);
        Assert.AreEqual(0, routines.UsedPercent);
    }
}
