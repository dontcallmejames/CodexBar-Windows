using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Gemini;

namespace CodexBar.Tests;

[TestClass]
public sealed class GeminiUsageMapperTests
{
    [TestMethod]
    public void MapsQuotaBucketsToProAndFlashWindows()
    {
        using var load = JsonDocument.Parse("""{ "tier": { "id": "standard-tier" } }""");
        using var quota = JsonDocument.Parse("""
        {
          "quota": [
            { "modelId": "gemini-2.5-pro", "remainingFraction": 0.25, "resetTime": "2026-05-07T12:00:00Z" },
            { "modelId": "gemini-2.5-flash", "remainingFraction": 0.80, "resetTime": "2026-05-07T13:00:00Z" }
          ]
        }
        """);

        var snapshot = GeminiUsageMapper.Map(
            load.RootElement,
            quota.RootElement,
            "gemini@example.com",
            DateTimeOffset.FromUnixTimeSeconds(1893440000));

        Assert.AreEqual(UsageProvider.Gemini, snapshot.Provider);
        Assert.AreEqual("Paid", snapshot.Plan);
        Assert.AreEqual("gemini@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Pro models", snapshot.Windows[0].Title);
        Assert.AreEqual(75, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual("Flash models", snapshot.Windows[1].Title);
        Assert.AreEqual(20, snapshot.Windows[1].UsedPercent);
    }
}
