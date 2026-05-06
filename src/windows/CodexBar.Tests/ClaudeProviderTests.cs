using System.Net;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers.Claude;

namespace CodexBar.Tests;

[TestClass]
public sealed class ClaudeProviderTests
{
    [TestMethod]
    public async Task MissingCredentialsReturnsMissingCredentialsWithoutHttpCall()
    {
        var credentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".credentials.json");
        using var handler = new QueueHandler();
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Claude, snapshot.Provider);
        Assert.IsTrue(snapshot.IsStale);
        Assert.AreEqual("none", snapshot.SourceLabel);
        Assert.IsNull(handler.Requests.SingleOrDefault());
    }

    [TestMethod]
    public async Task SendsOAuthUsageRequestHeaders()
    {
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "access-123",
            "refreshToken": "refresh-456",
            "scopes": ["user:profile"],
            "subscriptionType": "Pro"
          }
        }
        """);
        using var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "five_hour": { "utilization": 1, "resets_at": "2030-01-01T00:00:00Z" }
            }
            """)
        });
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.AreEqual(new Uri("https://api.anthropic.com/api/oauth/usage"), request.RequestUri);
        Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
        Assert.AreEqual("access-123", request.Headers.Authorization?.Parameter);
        Assert.IsTrue(request.Headers.TryGetValues("anthropic-beta", out var betaValues));
        Assert.AreEqual("oauth-2025-04-20", betaValues.Single());
        Assert.IsTrue(request.Headers.TryGetValues("User-Agent", out var userAgents));
        Assert.AreEqual("CodexBar-Windows", userAgents.Single());
        Assert.AreEqual("oauth", snapshot.SourceLabel);
        Assert.AreEqual("Pro", snapshot.Plan);
    }

    [TestMethod]
    public async Task ManualCookieFetchesOrganizationsThenUsage()
    {
        var credentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".credentials.json");
        using var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                [
                  { "uuid": "api-only-org", "name": "API Only Org", "capabilities": ["api"] },
                  { "uuid": "chat-org", "name": "Chat Org", "capabilities": ["chat"] }
                ]
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "five_hour": { "utilization": 4, "resets_at": "2030-01-01T00:00:00Z" },
                  "account": { "email": "claude@example.com", "subscription_type": "Team" },
                  "extra_usage": { "used_usd": 12.34 }
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), "sessionKey=sk-ant-123");

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(4, handler.Requests.Count);
        Assert.AreEqual(new Uri("https://claude.ai/api/organizations"), handler.Requests[0].RequestUri);
        Assert.AreEqual(new Uri("https://claude.ai/api/organizations/api-only-org/usage"), handler.Requests[1].RequestUri);
        Assert.AreEqual(new Uri("https://claude.ai/api/account"), handler.Requests[2].RequestUri);
        Assert.AreEqual(new Uri("https://claude.ai/api/organizations/api-only-org/overage_spend_limit"), handler.Requests[3].RequestUri);
        foreach (var request in handler.Requests)
        {
            Assert.IsTrue(request.Headers.TryGetValues("Cookie", out var cookies));
            Assert.AreEqual("sessionKey=sk-ant-123", cookies.Single());
            Assert.IsTrue(request.Headers.TryGetValues("User-Agent", out var userAgents));
            Assert.AreEqual("CodexBar-Windows", userAgents.Single());
        }

        Assert.AreEqual("web", snapshot.SourceLabel);
        Assert.AreEqual("claude@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Team", snapshot.Plan);
        Assert.AreEqual(12.34m, snapshot.TodayCostUsd);
    }

    [TestMethod]
    public async Task ManualCookieMergesAccountAndOverageWhenUsageOmitsThem()
    {
        var credentialsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".credentials.json");
        using var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                [
                  { "uuid": "org-123", "name": "Claude Org" }
                ]
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "five_hour": { "utilization": 4, "resets_at": "2030-01-01T00:00:00Z" }
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "email_address": "account@example.com",
                  "memberships": [
                    {
                      "organization": {
                        "uuid": "org-123",
                        "rate_limit_tier": "claude_team",
                        "billing_type": "stripe"
                      }
                    }
                  ]
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "is_enabled": true,
                  "used_credits": 1234,
                  "monthly_credit_limit": 5000,
                  "currency": "USD"
                }
                """)
            });
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), "sessionKey=sk-ant-123");

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(4, handler.Requests.Count);
        Assert.AreEqual(new Uri("https://claude.ai/api/account"), handler.Requests[2].RequestUri);
        Assert.AreEqual(new Uri("https://claude.ai/api/organizations/org-123/overage_spend_limit"), handler.Requests[3].RequestUri);
        Assert.AreEqual("account@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Team", snapshot.Plan);
        Assert.AreEqual(12.34m, snapshot.TodayCostUsd);
    }

    [TestMethod]
    public async Task OAuthWithoutUserProfileScopeFallsBackToManualCookie()
    {
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "access-123",
            "refreshToken": "refresh-456",
            "scopes": ["user:inference"]
          }
        }
        """);
        using var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                [
                  { "uuid": "org-123", "name": "Claude Org" }
                ]
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "five_hour": { "utilization": 4, "resets_at": "2030-01-01T00:00:00Z" }
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                { "email_address": "fallback@example.com" }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), "sessionKey=sk-ant-123");

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.IsFalse(handler.Requests.Any(request => request.RequestUri?.Host == "api.anthropic.com"));
        Assert.AreEqual(new Uri("https://claude.ai/api/organizations"), handler.Requests[0].RequestUri);
        Assert.AreEqual("web", snapshot.SourceLabel);
        Assert.AreEqual("fallback@example.com", snapshot.AccountEmail);
    }

    private static async Task<string> WriteCredentialsFileAsync(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".credentials.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;

        public QueueHandler(params HttpResponseMessage[] responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class TestAppPaths : IAppPaths
    {
        public TestAppPaths(string claudeCredentialsJson)
        {
            ClaudeCredentialsJson = claudeCredentialsJson;
        }

        public string SettingsFile => string.Empty;
        public string CacheDirectory => string.Empty;
        public string LogDirectory => string.Empty;
        public string ClaudeCredentialsJson { get; }
        public string CodexAuthJson(string? codexHome) => string.Empty;
    }
}
