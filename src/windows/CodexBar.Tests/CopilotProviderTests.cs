using System.Net;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Copilot;

namespace CodexBar.Tests;

[TestClass]
public sealed class CopilotProviderTests
{
    [TestMethod]
    public async Task ForbiddenResponseReturnsRequiresAuthenticationSnapshot()
    {
        using var handler = new StaticHandler(new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var httpClient = new HttpClient(handler);
        var provider = new CopilotProvider(httpClient, OkToken("ghp_token"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Copilot, snapshot.Provider);
        Assert.AreEqual(AuthState.RequiresAuthentication, snapshot.AuthState);
        Assert.IsTrue(snapshot.IsStale);
        StringAssert.Contains(snapshot.ErrorMessage!, "gh auth login");
    }

    [TestMethod]
    public async Task UnauthorizedResponseReturnsRequiresAuthenticationSnapshot()
    {
        using var handler = new StaticHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler);
        var provider = new CopilotProvider(httpClient, OkToken("ghp_token"));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(AuthState.RequiresAuthentication, snapshot.AuthState);
    }

    [TestMethod]
    public async Task TransientServerErrorPropagatesInsteadOfFakingSuccess()
    {
        // A 5xx after the token was accepted must propagate to the scheduler (which keeps
        // last-good data stale + backs off), NOT be swallowed into a MissingCredentials
        // snapshot that the scheduler would record as success.
        using var handler = new StaticHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var httpClient = new HttpClient(handler);
        var provider = new CopilotProvider(httpClient, OkToken("ghp_token"));

        await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => provider.RefreshAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task MissingTokenStaysAuthStateNone()
    {
        using var handler = new StaticHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var provider = new CopilotProvider(httpClient, _ => Task.FromResult(
            new CopilotTokenReader.TokenResult(null, CopilotTokenReader.TokenStatus.NotLoggedIn, "Run `gh auth login`.")));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(AuthState.None, snapshot.AuthState);
        Assert.IsTrue(snapshot.IsStale);
    }

    private static Func<CancellationToken, Task<CopilotTokenReader.TokenResult>> OkToken(string token) =>
        _ => Task.FromResult(new CopilotTokenReader.TokenResult(token, CopilotTokenReader.TokenStatus.Ok, null));

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage response;

        public StaticHandler(HttpResponseMessage response)
        {
            this.response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
