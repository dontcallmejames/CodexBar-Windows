using System.Net;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers.Codex;
using CodexBar.Core.Refresh;

namespace CodexBar.Tests;

[TestClass]
public sealed class CodexProviderTests
{
    [TestMethod]
    public async Task SendsOAuthUsageRequestHeaders()
    {
        var authPath = await WriteAuthFileAsync("""
        {
          "tokens": {
            "access_token": "access-123",
            "refresh_token": "refresh-456",
            "account_id": "account-789"
          }
        }
        """);
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "rate_limit": {
                "primary_window": { "used_percent": 1, "reset_at": 1893456000, "limit_window_seconds": 18000 }
              },
              "plan_type": "Pro"
            }
            """)
        });
        using var httpClient = new HttpClient(handler);
        var provider = new CodexProvider(httpClient, new TestAppPaths(authPath));

        await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(new Uri("https://chatgpt.com/backend-api/wham/usage"), handler.Request?.RequestUri);
        Assert.AreEqual("Bearer", handler.Request?.Headers.Authorization?.Scheme);
        Assert.AreEqual("access-123", handler.Request?.Headers.Authorization?.Parameter);
        Assert.IsTrue(handler.Request?.Headers.Accept.Any(value => value.MediaType == "application/json"));
        Assert.IsNotNull(handler.Request);
        Assert.IsTrue(handler.Request.Headers.TryGetValues("ChatGPT-Account-Id", out var accountIds));
        Assert.AreEqual("account-789", accountIds.Single());
    }

    [TestMethod]
    public async Task MissingAuthFileReturnsMissingCredentialsWithoutHttpCall()
    {
        var authPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "auth.json");
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var provider = new CodexProvider(httpClient, new TestAppPaths(authPath));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Codex, snapshot.Provider);
        Assert.IsTrue(snapshot.IsStale);
        Assert.AreEqual("none", snapshot.SourceLabel);
        Assert.IsNull(handler.Request);
    }

    [TestMethod]
    public async Task UnauthorizedResponseThrowsAuthenticationRequired()
    {
        var authPath = await WriteAuthFileAsync("""
        { "tokens": { "access_token": "access-123" } }
        """);
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler);
        var provider = new CodexProvider(httpClient, new TestAppPaths(authPath));

        var error = await Assert.ThrowsExactlyAsync<AuthenticationRequiredException>(
            () => provider.RefreshAsync(CancellationToken.None));
        StringAssert.Contains(error.Message, "codex login");
    }

    [TestMethod]
    public async Task ForbiddenResponseThrowsAuthenticationRequired()
    {
        var authPath = await WriteAuthFileAsync("""
        { "tokens": { "access_token": "access-123" } }
        """);
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var httpClient = new HttpClient(handler);
        var provider = new CodexProvider(httpClient, new TestAppPaths(authPath));

        await Assert.ThrowsExactlyAsync<AuthenticationRequiredException>(
            () => provider.RefreshAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ServerErrorStillThrowsHttpRequestException()
    {
        var authPath = await WriteAuthFileAsync("""
        { "tokens": { "access_token": "access-123" } }
        """);
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        var provider = new CodexProvider(httpClient, new TestAppPaths(authPath));

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => provider.RefreshAsync(CancellationToken.None));
    }

    private static async Task<string> WriteAuthFileAsync(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage response;

        public CapturingHandler(HttpResponseMessage response)
        {
            this.response = response;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(response);
        }
    }

    private sealed class TestAppPaths : IAppPaths
    {
        private readonly string codexAuthJson;

        public TestAppPaths(string codexAuthJson)
        {
            this.codexAuthJson = codexAuthJson;
        }

        public string SettingsFile => string.Empty;
        public string CacheDirectory => string.Empty;
        public string LogDirectory => string.Empty;
        public string ClaudeCredentialsJson => string.Empty;
        public string GeminiSettingsJson => string.Empty;
        public string GeminiOAuthCredentialsJson => string.Empty;
        public string CodexAuthJson(string? codexHome) => codexAuthJson;
    }
}
