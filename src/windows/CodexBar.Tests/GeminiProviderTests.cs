using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers.Gemini;
using CodexBar.Core.Refresh;
using System.Net;

namespace CodexBar.Tests;

[TestClass]
public sealed class GeminiProviderTests
{
    [TestMethod]
    public async Task MissingCredentialsReturnsMissingCredentialsSnapshot()
    {
        var paths = WindowsAppPaths.ForTest(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), Path.GetTempPath());
        var provider = new GeminiProvider(new HttpClient(new QueueHandler()), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Gemini, snapshot.Provider);
        StringAssert.Contains(snapshot.ErrorMessage!, "Gemini CLI");
    }

    [TestMethod]
    public async Task UnsupportedApiKeyModeReturnsUnsupportedSourceSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiSettingsJson)!);
        await File.WriteAllTextAsync(paths.GeminiSettingsJson, """{ "selectedAuthType": "api-key" }""");
        var provider = new GeminiProvider(new HttpClient(new QueueHandler()), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        StringAssert.Contains(snapshot.ErrorMessage!, "Gemini CLI OAuth");
    }

    [TestMethod]
    public async Task SendsBearerTokenToCodeAssistQuotaApis()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        await File.WriteAllTextAsync(paths.GeminiOAuthCredentialsJson, """
        {
          "access_token": "access",
          "refresh_token": "refresh",
          "id_token": "header.eyJlbWFpbCI6ImdlbWluaUBleGFtcGxlLmNvbSJ9.signature",
          "expiry_date": 1893440000000
        }
        """);
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "currentTier": { "id": "standard-tier" }, "cloudaicompanionProject": "project-123" }""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "buckets": [
                    { "modelId": "gemini-2.5-pro", "remainingFraction": 0.25, "resetTime": "2026-05-07T12:00:00Z" }
                  ]
                }
                """)
            });
        var provider = new GeminiProvider(new HttpClient(handler), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual("gemini@example.com", snapshot.AccountEmail);
        Assert.AreEqual("Paid", snapshot.Plan);
        Assert.AreEqual("Pro models", snapshot.Windows.Single().Title);
        CollectionAssert.AreEqual(
            new[]
            {
                new Uri("https://cloudcode-pa.googleapis.com/v1internal:loadCodeAssist"),
                new Uri("https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota")
            },
            handler.Requests.Select(request => request.RequestUri).ToArray());
        Assert.IsTrue(handler.Requests.All(request => request.Headers.Authorization?.Scheme == "Bearer"));
        Assert.IsTrue(handler.Requests.All(request => request.Headers.Authorization?.Parameter == "access"));
        StringAssert.Contains(handler.RequestBodies[1]!, "project-123");
    }

    [TestMethod]
    public async Task RetrievesQuotaWithEmptyBodyWhenProjectIsUnknown()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        await File.WriteAllTextAsync(paths.GeminiOAuthCredentialsJson, """
        {
          "access_token": "access",
          "refresh_token": "refresh",
          "id_token": "header.eyJlbWFpbCI6ImdlbWluaUBleGFtcGxlLmNvbSJ9.signature",
          "expiry_date": 1893440000000
        }
        """);
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "currentTier": { "id": "standard-tier" } }""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "buckets": [
                    { "modelId": "gemini-2.5-pro", "remainingFraction": 0.40, "resetTime": "2026-05-07T12:00:00Z" }
                  ]
                }
                """)
            });
        var provider = new GeminiProvider(new HttpClient(handler), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual("Pro models", snapshot.Windows.Single().Title);
        CollectionAssert.AreEqual(
            new[]
            {
                new Uri("https://cloudcode-pa.googleapis.com/v1internal:loadCodeAssist"),
                new Uri("https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota")
            },
            handler.Requests.Select(request => request.RequestUri).ToArray());
        Assert.AreEqual("{}", handler.RequestBodies[1]);
    }

    [TestMethod]
    public async Task ExpiredCredentialsWithoutClientMetadataReturnMissingCredentialsSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        await File.WriteAllTextAsync(paths.GeminiOAuthCredentialsJson, """
        {
          "access_token": "expired",
          "refresh_token": "refresh",
          "expiry_date": 1
        }
        """);
        var handler = new QueueHandler();
        var provider = new GeminiProvider(new HttpClient(handler), paths, new StaticGeminiOAuthClient(null));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.IsTrue(snapshot.IsStale);
        StringAssert.Contains(snapshot.ErrorMessage!, "OAuth client");
        Assert.AreEqual(0, handler.Requests.Count);
    }

    [TestMethod]
    public async Task ExpiredCredentialsRefreshTokenAndPersistUpdatedCredentials()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        await File.WriteAllTextAsync(paths.GeminiOAuthCredentialsJson, """
        {
          "access_token": "expired",
          "refresh_token": "refresh",
          "id_token": "header.eyJlbWFpbCI6ImdlbWluaUBleGFtcGxlLmNvbSJ9.signature",
          "expiry_date": 1
        }
        """);
        var handler = new QueueHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "access_token": "fresh", "refresh_token": "fresh-refresh", "expires_in": 3600 }""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "currentTier": { "id": "standard-tier" }, "cloudaicompanionProject": "project-123" }""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "buckets": [] }""")
            });
        var provider = new GeminiProvider(new HttpClient(handler), paths, new StaticGeminiOAuthClient("id", "secret"));

        await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(new Uri("https://oauth2.googleapis.com/token"), handler.Requests[0].RequestUri);
        Assert.AreEqual("fresh", handler.Requests[1].Headers.Authorization?.Parameter);
        var updatedCredentials = await File.ReadAllTextAsync(paths.GeminiOAuthCredentialsJson);
        StringAssert.Contains(updatedCredentials, "fresh");
        StringAssert.Contains(updatedCredentials, "fresh-refresh");
    }

    [TestMethod]
    public async Task OAuthClientProviderReadsBundledGeminiCliChunks()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var bundle = Path.Combine(root, "bundle");
            Directory.CreateDirectory(bundle);
            await File.WriteAllTextAsync(Path.Combine(bundle, "chunk-test.js"), """
            // packages/core/dist/src/code_assist/oauth2.js
            var OAUTH_CLIENT_ID = "bundled-client-id";
            var OAUTH_CLIENT_SECRET = "bundled-client-secret";
            """);

            var client = await GeminiOAuthClientProvider.ReadClientFromRootsAsync(new[] { root }, CancellationToken.None);

            Assert.AreEqual("bundled-client-id", client?.ClientId);
            Assert.AreEqual("bundled-client-secret", client?.ClientSecret);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task RefreshTokenFailureReturnsConciseAuthSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        await File.WriteAllTextAsync(paths.GeminiOAuthCredentialsJson, """
        {
          "access_token": "expired",
          "refresh_token": "refresh",
          "expiry_date": 1
        }
        """);
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{ "error": "invalid_grant" }""")
        });
        var provider = new GeminiProvider(new HttpClient(handler), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.IsTrue(snapshot.IsStale);
        Assert.AreEqual("none", snapshot.SourceLabel);
        StringAssert.Contains(snapshot.ErrorMessage!, "Gemini CLI OAuth refresh failed");
    }

    [TestMethod]
    public async Task UnauthorizedFromCodeAssistReturnsRetirementMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        await File.WriteAllTextAsync(paths.GeminiOAuthCredentialsJson, """
        {
          "access_token": "access",
          "refresh_token": "refresh",
          "id_token": "header.eyJlbWFpbCI6ImdlbWluaUBleGFtcGxlLmNvbSJ9.signature",
          "expiry_date": 1893440000000
        }
        """);
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var provider = new GeminiProvider(new HttpClient(handler), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        StringAssert.Contains(snapshot.ErrorMessage!, "retired June 18, 2026");
        StringAssert.Contains(snapshot.ErrorMessage!, "Antigravity");
    }

    [TestMethod]
    public async Task ForbiddenFromCodeAssistReturnsRetirementMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        await File.WriteAllTextAsync(paths.GeminiOAuthCredentialsJson, """
        {
          "access_token": "access",
          "refresh_token": "refresh",
          "id_token": "header.eyJlbWFpbCI6ImdlbWluaUBleGFtcGxlLmNvbSJ9.signature",
          "expiry_date": 1893440000000
        }
        """);
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.Forbidden));
        var provider = new GeminiProvider(new HttpClient(handler), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        StringAssert.Contains(snapshot.ErrorMessage!, "retired June 18, 2026");
        StringAssert.Contains(snapshot.ErrorMessage!, "Antigravity");
    }

    [TestMethod]
    public async Task ReturnsRetirementMessageWhenQuotaCallIsForbidden()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
        // Valid, non-expired credentials so the provider proceeds to the quota call.
        await File.WriteAllTextAsync(paths.GeminiOAuthCredentialsJson, """
        {
          "access_token": "live-token",
          "refresh_token": "refresh",
          "expiry_date": 1893440000000
        }
        """);
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.Forbidden));
        var provider = new GeminiProvider(new HttpClient(handler), paths, new StaticGeminiOAuthClient("id", "secret"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        StringAssert.Contains(snapshot.ErrorMessage!, "retired June 18, 2026");
        StringAssert.Contains(snapshot.ErrorMessage!, "Antigravity");
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
            RequestBodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return responses.Count == 0 ? new HttpResponseMessage(HttpStatusCode.OK) : responses.Dequeue();
        }
    }

    private sealed class StaticGeminiOAuthClient : IGeminiOAuthClientProvider
    {
        private readonly (string ClientId, string ClientSecret)? client;

        public StaticGeminiOAuthClient(string clientId, string clientSecret)
        {
            client = (clientId, clientSecret);
        }

        public StaticGeminiOAuthClient((string ClientId, string ClientSecret)? client)
        {
            this.client = client;
        }

        public Task<(string ClientId, string ClientSecret)?> ReadClientAsync(CancellationToken cancellationToken) =>
            Task.FromResult(client);
    }
}
