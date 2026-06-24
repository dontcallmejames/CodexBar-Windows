using System.Net;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers.Claude;
using CodexBar.Core.Refresh;

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
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

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
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.AreEqual(new Uri("https://api.anthropic.com/api/oauth/usage"), request.RequestUri);
        Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
        Assert.AreEqual("access-123", request.Headers.Authorization?.Parameter);
        Assert.IsTrue(request.Headers.TryGetValues("anthropic-beta", out var betaValues));
        Assert.AreEqual("oauth-2025-04-20", betaValues.Single());
        Assert.IsTrue(request.Headers.TryGetValues("User-Agent", out var userAgents));
        Assert.AreEqual("claude-code/2.1.0", userAgents.Single());
        Assert.AreEqual("oauth", snapshot.SourceLabel);
        Assert.AreEqual("Pro", snapshot.Plan);
    }

    [TestMethod]
    public async Task OAuthUnauthorizedRefreshesTokenAndRetriesUsage()
    {
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "expired-access",
            "refreshToken": "refresh-456",
            "expiresAt": 1,
            "scopes": ["user:profile"],
            "subscriptionType": "Max",
            "rateLimitTier": "default_claude_max_5x"
          }
        }
        """);
        using var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "access_token": "fresh-access",
                  "refresh_token": "fresh-refresh",
                  "expires_in": 3600
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "five_hour": { "utilization": 7, "resets_at": "2030-01-01T00:00:00Z" }
                }
                """)
            });
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(3, handler.Requests.Count);
        Assert.AreEqual(new Uri("https://api.anthropic.com/api/oauth/usage"), handler.Requests[0].RequestUri);
        Assert.AreEqual(new Uri("https://platform.claude.com/v1/oauth/token"), handler.Requests[1].RequestUri);
        Assert.AreEqual("application/x-www-form-urlencoded", handler.Requests[1].Content!.Headers.ContentType!.MediaType);
        var refreshBody = handler.RequestBodies[1];
        StringAssert.Contains(refreshBody, "grant_type=refresh_token");
        StringAssert.Contains(refreshBody, "refresh_token=refresh-456");
        StringAssert.Contains(refreshBody, "client_id=9d1c250a-e61b-44d9-88ed-5944d1962f5e");
        Assert.AreEqual("fresh-access", handler.Requests[2].Headers.Authorization!.Parameter);
        Assert.AreEqual(7, snapshot.Windows.Single().UsedPercent);
        Assert.AreEqual("Max", snapshot.Plan);

        var updatedJson = await File.ReadAllTextAsync(credentialsPath);
        StringAssert.Contains(updatedJson, "fresh-access");
        StringAssert.Contains(updatedJson, "fresh-refresh");
    }

    [TestMethod]
    public async Task OAuthUnauthorizedWithoutRefreshTokenThrowsAuthenticationRequired()
    {
        // The motivating bug: a future expiresAt but an EMPTY refresh token. The 401 must
        // surface as a re-auth signal, not silently give up and keep an empty snapshot.
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "access-123",
            "refreshToken": "",
            "expiresAt": 9999999999999,
            "scopes": ["user:profile"]
          }
        }
        """);
        using var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

        var error = await Assert.ThrowsExactlyAsync<AuthenticationRequiredException>(
            () => provider.RefreshAsync(CancellationToken.None));

        StringAssert.Contains(error.Message, "/login");
        Assert.AreEqual(1, handler.Requests.Count, "must not attempt a refresh with no refresh token");
    }

    [TestMethod]
    public async Task OAuthRefreshThatStillReturnsUnauthorizedThrowsAuthenticationRequired()
    {
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "expired-access",
            "refreshToken": "refresh-456",
            "expiresAt": 1,
            "scopes": ["user:profile"]
          }
        }
        """);
        using var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                { "access_token": "fresh-access", "refresh_token": "fresh-refresh", "expires_in": 3600 }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

        await Assert.ThrowsExactlyAsync<AuthenticationRequiredException>(
            () => provider.RefreshAsync(CancellationToken.None));
        Assert.AreEqual(3, handler.Requests.Count);
    }

    [TestMethod]
    public async Task OAuthRefreshInvalidGrantThrowsAuthenticationRequired()
    {
        // A revoked refresh token: usage 401, then the refresh endpoint rejects the refresh
        // token with 400 invalid_grant. Must surface as re-auth, NOT fall through to the
        // scheduler's transient path (which would keep stale AuthState.None data and never
        // show the reconnect prompt).
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "expired-access",
            "refreshToken": "revoked-refresh",
            "expiresAt": 1,
            "scopes": ["user:profile"]
          }
        }
        """);
        using var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{ "error": "invalid_grant" }""")
            });
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

        await Assert.ThrowsExactlyAsync<AuthenticationRequiredException>(
            () => provider.RefreshAsync(CancellationToken.None));
        Assert.AreEqual(2, handler.Requests.Count);
    }

    [TestMethod]
    public async Task OAuthRefreshMissingAccessTokenThrowsAuthenticationRequired()
    {
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "expired-access",
            "refreshToken": "refresh-456",
            "expiresAt": 1,
            "scopes": ["user:profile"]
          }
        }
        """);
        using var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "refresh_token": "fresh-refresh", "expires_in": 3600 }""")
            });
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

        await Assert.ThrowsExactlyAsync<AuthenticationRequiredException>(
            () => provider.RefreshAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task OAuthThrottleThrowsRateLimitException()
    {
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "access-123",
            "refreshToken": "refresh-456",
            "scopes": ["user:profile"]
          }
        }
        """);
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.TryAddWithoutValidation("Retry-After", "120");
        using var handler = new QueueHandler(response);
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

        var error = await Assert.ThrowsExactlyAsync<RateLimitException>(
            () => provider.RefreshAsync(CancellationToken.None));

        StringAssert.Contains(error.Message, "Claude API rate-limited");
        Assert.AreEqual(TimeSpan.FromSeconds(120), error.RetryAfter);
    }

    [TestMethod]
    public async Task ServiceUnavailable_ThrowsRateLimitExceptionWithRetryAfter()
    {
        var credentialsPath = await WriteCredentialsFileAsync("""
        {
          "claudeAiOauth": {
            "accessToken": "access-123",
            "refreshToken": "refresh-456",
            "scopes": ["user:profile"]
          }
        }
        """);
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        response.Headers.TryAddWithoutValidation("Retry-After", "45");
        using var handler = new QueueHandler(response);
        using var httpClient = new HttpClient(handler);
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), localUsageScanner: EmptyScanner());

        var ex = await Assert.ThrowsExactlyAsync<RateLimitException>(
            () => provider.RefreshAsync(CancellationToken.None));

        Assert.AreEqual(TimeSpan.FromSeconds(45), ex.RetryAfter);
        StringAssert.Contains(ex.Message, "503");
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
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), "sessionKey=sk-ant-123", EmptyScanner());

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
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), "sessionKey=sk-ant-123", EmptyScanner());

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(4, handler.Requests.Count);
        Assert.AreEqual(new Uri("https://claude.ai/api/account"), handler.Requests[2].RequestUri);
        Assert.AreEqual(new Uri("https://claude.ai/api/organizations/org-123/overage_spend_limit"), handler.Requests[3].RequestUri);
        Assert.AreEqual("account@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Team", snapshot.Plan);
        Assert.AreEqual(12.34m, snapshot.TodayCostUsd);
    }

    [TestMethod]
    public async Task ManualCookieFillsMissingFieldsFromAccountAndOverage()
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
                  "five_hour": { "utilization": 4, "resets_at": "2030-01-01T00:00:00Z" },
                  "account": { "email": "usage@example.com" },
                  "extra_usage": { "is_enabled": true }
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
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), "sessionKey=sk-ant-123", EmptyScanner());

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual("usage@example.com", snapshot.AccountEmail);
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
        var provider = new ClaudeProvider(httpClient, new TestAppPaths(credentialsPath), "sessionKey=sk-ant-123", EmptyScanner());

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.IsFalse(handler.Requests.Any(request => request.RequestUri?.Host == "api.anthropic.com"));
        Assert.AreEqual(new Uri("https://claude.ai/api/organizations"), handler.Requests[0].RequestUri);
        Assert.AreEqual("web", snapshot.SourceLabel);
        Assert.AreEqual("fallback@example.com", snapshot.AccountEmail);
    }

    // The default ClaudeProvider scanner walks %USERPROFILE%\.claude\projects.
    // Tests inject a scanner rooted at a non-existent path so local session data
    // on the developer/CI machine cannot leak into snapshot assertions.
    private static ClaudeCodeLocalUsageScanner EmptyScanner() =>
        new(Path.Combine(Path.GetTempPath(), "no-such-claude-projects-" + Guid.NewGuid().ToString("N")));

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
        public List<string?> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return responses.Dequeue();
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
        public string GeminiSettingsJson => string.Empty;
        public string GeminiOAuthCredentialsJson => string.Empty;
        public string CodexAuthJson(string? codexHome) => string.Empty;
    }
}
