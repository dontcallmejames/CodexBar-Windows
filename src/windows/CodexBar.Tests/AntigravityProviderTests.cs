using System.Net;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Antigravity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class AntigravityProviderTests
{
    private sealed class FakeLocator(IReadOnlyList<AntigravityCandidate> candidates) : IAntigravityProcessLocator
    {
        public IReadOnlyList<AntigravityCandidate> FindCandidates() => candidates;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private static AntigravityCandidate Cli() =>
        new(Pid: 1, LoopbackPorts: [42100], CsrfToken: "", ExtensionServerPort: null, ExtensionServerCsrfToken: null, IsCli: true);

    [TestMethod]
    public async Task ReturnsNotRunningWhenNoCandidates()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var provider = new AntigravityProvider(http, new FakeLocator([]));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(0, snapshot.Windows.Count);
        Assert.AreEqual("Antigravity isn't running.", snapshot.ErrorMessage);
    }

    [TestMethod]
    public async Task ThrowsWhenCandidatesButNoQuota()
    {
        // Candidates exist but every RPC fails — a transient failure. Throwing (rather than
        // returning an error snapshot) lets RefreshScheduler preserve the last-good snapshot as stale.
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var provider = new AntigravityProvider(http, new FakeLocator([Cli()]));

        await Assert.ThrowsExactlyAsync<AntigravityUnavailableException>(
            () => provider.RefreshAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task FillsPlanAndEmailFromUserStatusWhenSummaryLacksThem()
    {
        using var http = new HttpClient(new StubHandler(request =>
        {
            var uri = request.RequestUri!.AbsoluteUri;
            if (uri.EndsWith("RetrieveUserQuotaSummary", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {"groups":[{"buckets":[
                      {"bucketId":"gemini-pro","displayName":"Gemini 3 Pro","remainingFraction":0.4,"resetTime":"2030-01-01T00:00:00Z","disabled":false}
                    ]}]}
                    """),
                };
            }

            if (uri.EndsWith("GetUserStatus", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {"accountEmail":"jim@example.com","userStatus":{"userTier":{"preferredName":"Google AI Ultra"}}}
                    """),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }));
        var provider = new AntigravityProvider(http, new FakeLocator([Cli()]));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual("Google AI Ultra", snapshot.Plan);
        Assert.AreEqual("jim@example.com", snapshot.AccountEmail);
        Assert.AreEqual(60.0, snapshot.Windows.Single(w => w.Title == "Gemini Pro").UsedPercent, 0.001);
    }

    [TestMethod]
    public async Task MapsQuotaWhenServerResponds()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {"groups":[{"buckets":[
              {"bucketId":"gemini-pro","displayName":"Gemini 3 Pro","remainingFraction":0.4,"resetTime":"2030-01-01T00:00:00Z","disabled":false}
            ]}]}
            """)
        }));
        var provider = new AntigravityProvider(http, new FakeLocator([Cli()]));

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Antigravity, snapshot.Provider);
        Assert.AreEqual(60.0, snapshot.Windows.Single(w => w.Title == "Gemini Pro").UsedPercent, 0.001);
        Assert.IsNull(snapshot.ErrorMessage);
    }
}
