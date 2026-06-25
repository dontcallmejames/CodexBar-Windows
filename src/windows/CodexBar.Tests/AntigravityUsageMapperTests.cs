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
    public void MapsWrappedGroupedQuotaSummary()
    {
        // Real shape from a live Antigravity server: a Connect "response" envelope wrapping
        // groups (Gemini vs Claude/GPT), each with weekly + 5-hour window buckets.
        var root = Parse("""
        {"response":{"groups":[
          {"displayName":"Gemini Models","buckets":[
            {"bucketId":"gemini-weekly","displayName":"Weekly Limit","remainingFraction":1.0,"resetTime":"2030-01-01T00:00:00Z"},
            {"bucketId":"gemini-5h","displayName":"Five Hour Limit","remainingFraction":0.5,"resetTime":"2030-01-01T00:00:00Z"}]},
          {"displayName":"Claude and GPT models","buckets":[
            {"bucketId":"3p-weekly","displayName":"Weekly Limit","remainingFraction":0.8,"resetTime":"2030-01-01T00:00:00Z"}]}]}}
        """);

        var snapshot = AntigravityUsageMapper.Map("RetrieveUserQuotaSummary", root, DateTimeOffset.UnixEpoch);

        Assert.AreEqual(UsageProvider.Antigravity, snapshot.Provider);
        Assert.IsNull(snapshot.ErrorMessage);
        Assert.AreEqual(3, snapshot.Windows.Count);
        Assert.AreEqual(0.0, snapshot.Windows.Single(w => w.Title == "Gemini · Weekly Limit").UsedPercent, 0.001);
        Assert.AreEqual(50.0, snapshot.Windows.Single(w => w.Title == "Gemini · Five Hour Limit").UsedPercent, 0.001);
        Assert.AreEqual(20.0, snapshot.Windows.Single(w => w.Title == "Claude and GPT · Weekly Limit").UsedPercent, 0.001);
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
    public void ReadIdentityExtractsPlanAndEmailFromUserStatus()
    {
        var root = Parse("""
        {"accountEmail":"jim@example.com","userStatus":{"userTier":{"preferredName":"Google AI Ultra"}}}
        """);

        var (plan, email) = AntigravityUsageMapper.ReadIdentity(root);

        Assert.AreEqual("Google AI Ultra", plan);
        Assert.AreEqual("jim@example.com", email);
    }

    [TestMethod]
    public void MapsRealUserStatusShape_EmailUnderUserStatus_PlanFromPlanInfoPlanName()
    {
        // Shape observed from a live Antigravity build: email lives at userStatus.email and the
        // plan name at userStatus.planStatus.planInfo.planName (no accountEmail / userTier).
        var root = Parse("""
        {
          "userStatus": {
            "name": "Jim",
            "email": "jim@example.com",
            "planStatus": { "planInfo": { "planName": "Pro" } },
            "cascadeModelConfigData": {
              "clientModelConfigs": [
                { "label": "Gemini 3.1 Pro (Low)", "modelOrAlias": { "model": "M36" }, "quotaInfo": { "remainingFraction": 1.0, "resetTime": "2030-01-01T00:00:00Z" } },
                { "label": "Claude Sonnet 4.6 (Thinking)", "modelOrAlias": { "model": "M35" }, "quotaInfo": { "remainingFraction": 0.5, "resetTime": "2030-01-01T00:00:00Z" } }
              ]
            }
          }
        }
        """);

        var snapshot = AntigravityUsageMapper.Map("GetUserStatus", root, DateTimeOffset.UnixEpoch);

        Assert.AreEqual("jim@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Pro", snapshot.Plan);
        Assert.AreEqual(0.0, snapshot.Windows.Single(w => w.Title == "Gemini Pro").UsedPercent, 0.001);
        Assert.AreEqual(50.0, snapshot.Windows.Single(w => w.Title == "Claude").UsedPercent, 0.001);
    }

    [TestMethod]
    public void ReadIdentityReadsRealUserStatusShape()
    {
        var root = Parse("""
        {"userStatus":{"email":"jim@example.com","planStatus":{"planInfo":{"planName":"Pro"}}}}
        """);

        var (plan, email) = AntigravityUsageMapper.ReadIdentity(root);

        Assert.AreEqual("Pro", plan);
        Assert.AreEqual("jim@example.com", email);
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
