using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Antigravity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class AntigravityUsageMapperTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [TestMethod]
    public void MapsQuotaSummaryToThreeLanes()
    {
        var root = Parse("""
        {
          "groups": [
            { "displayName": "Models", "buckets": [
              { "bucketId": "claude", "displayName": "Claude Sonnet", "remainingFraction": 0.25, "resetTime": "2030-01-01T00:00:00Z", "disabled": false },
              { "bucketId": "gemini-pro", "displayName": "Gemini 3 Pro", "remainingFraction": 0.80, "resetTime": "2030-01-01T00:00:00Z", "disabled": false },
              { "bucketId": "gemini-flash", "displayName": "Gemini Flash", "remainingFraction": 1.0, "resetTime": "2030-01-01T00:00:00Z", "disabled": false }
            ] }
          ]
        }
        """);

        var snapshot = AntigravityUsageMapper.Map("RetrieveUserQuotaSummary", root, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(UsageProvider.Antigravity, snapshot.Provider);
        Assert.AreEqual(3, snapshot.Windows.Count);
        var claude = snapshot.Windows.Single(w => w.Title == "Claude");
        Assert.AreEqual(75.0, claude.UsedPercent, 0.001);
        var pro = snapshot.Windows.Single(w => w.Title == "Gemini Pro");
        Assert.AreEqual(20.0, pro.UsedPercent, 0.001);
        Assert.IsNull(snapshot.ErrorMessage);
    }

    [TestMethod]
    public void SkipsDisabledBuckets()
    {
        var root = Parse("""
        {
          "groups": [
            { "buckets": [
              { "bucketId": "claude", "displayName": "Claude", "remainingFraction": 0.5, "disabled": true }
            ] }
          ]
        }
        """);

        var snapshot = AntigravityUsageMapper.Map("RetrieveUserQuotaSummary", root, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(0, snapshot.Windows.Count);
        Assert.AreEqual("Limits not available", snapshot.ErrorMessage);
        Assert.IsTrue(snapshot.IsStale);
    }

    [TestMethod]
    public void MapsUserStatusLanesPlanAndEmail()
    {
        var root = Parse("""
        {
          "accountEmail": "jim@example.com",
          "userStatus": {
            "userTier": { "preferredName": "Google AI Ultra" },
            "cascadeModelConfigData": {
              "clientModelConfigs": [
                { "label": "Claude Opus", "modelOrAlias": { "model": "claude-opus" }, "quotaInfo": { "remainingFraction": 0.10, "resetTime": "2030-01-01T00:00:00Z" } },
                { "label": "Gemini 3 Pro", "modelOrAlias": { "model": "gemini-3-pro" }, "quotaInfo": { "remainingFraction": 0.90, "resetTime": "2030-01-01T00:00:00Z" } }
              ]
            }
          }
        }
        """);

        var snapshot = AntigravityUsageMapper.Map("GetUserStatus", root, DateTimeOffset.UnixEpoch);

        Assert.AreEqual("jim@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Google AI Ultra", snapshot.Plan);
        Assert.AreEqual(90.0, snapshot.Windows.Single(w => w.Title == "Claude").UsedPercent, 0.001);
        Assert.AreEqual(10.0, snapshot.Windows.Single(w => w.Title == "Gemini Pro").UsedPercent, 0.001);
    }
}
