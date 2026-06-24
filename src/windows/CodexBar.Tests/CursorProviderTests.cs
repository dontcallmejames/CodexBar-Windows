using System.Net;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Cursor;

namespace CodexBar.Tests;

[TestClass]
public sealed class CursorProviderTests
{
    [TestMethod]
    public async Task MissingCookieReturnsMissingCredentialsSnapshot()
    {
        var provider = new CursorProvider(new HttpClient(new QueueHandler()), null);

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(UsageProvider.Cursor, snapshot.Provider);
        Assert.IsTrue(snapshot.IsStale);
        StringAssert.Contains(snapshot.ErrorMessage!, "Cursor cookie");
    }

    [TestMethod]
    public async Task SendsCookieHeaderToCursorApis()
    {
        using var handler = new QueueHandler(
            (new Uri("https://cursor.com/api/usage-summary"), """{ "includedUsage": 10, "includedUsageLimit": 100 }"""),
            (new Uri("https://cursor.com/api/auth/me"), """{ "email": "cursor@example.com" }"""));
        var provider = new CursorProvider(new HttpClient(handler), "WorkosCursorSessionToken=abc");

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual("cursor@example.com", snapshot.AccountEmail);
        Assert.IsTrue(handler.Requests.All(request => request.Headers.GetValues("Cookie").Single() == "WorkosCursorSessionToken=abc"));
    }

    [TestMethod]
    public async Task UnauthorizedResponseReturnsRequiresAuthenticationSnapshot()
    {
        using var handler = new QueueHandler(HttpStatusCode.Unauthorized, "{}");
        var provider = new CursorProvider(new HttpClient(handler), "expired=true");

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(AuthState.RequiresAuthentication, snapshot.AuthState);
        Assert.IsTrue(snapshot.IsStale);
        StringAssert.Contains(snapshot.ErrorMessage!, "Cursor rejected your saved cookie");
    }

    [TestMethod]
    public async Task ForbiddenResponseReturnsRequiresAuthenticationSnapshot()
    {
        using var handler = new QueueHandler(HttpStatusCode.Forbidden, "{}");
        var provider = new CursorProvider(new HttpClient(handler), "expired=true");

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(AuthState.RequiresAuthentication, snapshot.AuthState);
        StringAssert.Contains(snapshot.ErrorMessage!, "WorkosCursorSessionToken");
    }

    [TestMethod]
    public async Task MissingCookieStaysAuthStateNone()
    {
        var provider = new CursorProvider(new HttpClient(new QueueHandler()), null);

        var snapshot = await provider.RefreshAsync(CancellationToken.None);

        Assert.AreEqual(AuthState.None, snapshot.AuthState);
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new();

        public QueueHandler()
        {
        }

        public QueueHandler(params (Uri Uri, string Body)[] responses)
        {
            foreach (var (_, body) in responses)
            {
                this.responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body)
                });
            }
        }

        public QueueHandler(HttpStatusCode statusCode, string body)
        {
            responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            });
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses.Count > 0 ? responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
