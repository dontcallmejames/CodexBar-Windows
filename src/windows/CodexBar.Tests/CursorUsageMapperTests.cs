using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Cursor;

namespace CodexBar.Tests;

[TestClass]
public sealed class CursorUsageMapperTests
{
    [TestMethod]
    public void MapsUsageSummaryIntoPlanAndOnDemandWindows()
    {
        using var usage = JsonDocument.Parse("""
        {
          "includedUsage": 75,
          "includedUsageLimit": 500,
          "onDemandUsage": 12.5,
          "onDemandUsageLimit": 50,
          "billingCycleEnd": "2026-05-30T00:00:00Z"
        }
        """);
        using var account = JsonDocument.Parse("""{ "email": "cursor@example.com", "name": "Cursor User" }""");

        var snapshot = CursorUsageMapper.Map(
            usage.RootElement,
            account.RootElement,
            DateTimeOffset.FromUnixTimeSeconds(1893440000));

        Assert.AreEqual(UsageProvider.Cursor, snapshot.Provider);
        Assert.AreEqual("Cursor", snapshot.DisplayName);
        Assert.AreEqual("cursor@example.com", snapshot.AccountEmail);
        Assert.AreEqual(2, snapshot.Windows.Count);
        Assert.AreEqual("Included plan", snapshot.Windows[0].Title);
        Assert.AreEqual(15, snapshot.Windows[0].UsedPercent);
        Assert.AreEqual("On-demand", snapshot.Windows[1].Title);
        Assert.AreEqual(25, snapshot.Windows[1].UsedPercent);
        Assert.AreEqual(DateTimeOffset.Parse("2026-05-30T00:00:00Z"), snapshot.Windows[0].ResetsAt);
    }

    [TestMethod]
    public void UnknownUsageShapeReturnsNoUsageDataSnapshot()
    {
        using var usage = JsonDocument.Parse("""{ "unexpected": true }""");

        var snapshot = CursorUsageMapper.Map(
            usage.RootElement,
            null,
            DateTimeOffset.FromUnixTimeSeconds(1893440000));

        Assert.AreEqual(UsageProvider.Cursor, snapshot.Provider);
        Assert.AreEqual(0, snapshot.Windows.Count);
        Assert.AreEqual("No usage data", snapshot.ErrorMessage);
        Assert.IsTrue(snapshot.IsStale);
    }
}
