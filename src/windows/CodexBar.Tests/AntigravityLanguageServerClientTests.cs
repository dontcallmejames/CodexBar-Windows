using System.Net;
using CodexBar.Core.Providers.Antigravity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class AntigravityLanguageServerClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
        public List<HttpRequestMessage> Requests { get; } = [];

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => this.responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private static AntigravityCandidate Candidate(string token = "tok") =>
        new(Pid: 1, LoopbackPorts: [42100], CsrfToken: token, ExtensionServerPort: null, ExtensionServerCsrfToken: null, IsCli: false);

    [TestMethod]
    public async Task SendsCsrfHeaderAndConnectVersion_AndReturnsFirstSuccess()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"groups":[]}""")
        });
        using var http = new HttpClient(handler);
        var client = new AntigravityLanguageServerClient(http);

        using var response = await client.FetchAsync(Candidate(), CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.AreEqual("RetrieveUserQuotaSummary", response!.Method);
        var first = handler.Requests[0];
        Assert.IsTrue(first.RequestUri!.AbsoluteUri.EndsWith("/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary", StringComparison.Ordinal));
        Assert.AreEqual("tok", first.Headers.GetValues("X-Codeium-Csrf-Token").Single());
        Assert.AreEqual("1", first.Headers.GetValues("Connect-Protocol-Version").Single());
    }

    [TestMethod]
    public async Task FallsBackToGetUserStatusWhenSummaryFails()
    {
        using var handler = new StubHandler(request =>
        {
            var ok = request.RequestUri!.AbsoluteUri.EndsWith("GetUserStatus", StringComparison.Ordinal);
            return new HttpResponseMessage(ok ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            {
                Content = new StringContent(ok ? """{"userStatus":{}}""" : "nope")
            };
        });
        using var http = new HttpClient(handler);
        var client = new AntigravityLanguageServerClient(http);

        using var response = await client.FetchAsync(Candidate(), CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.AreEqual("GetUserStatus", response!.Method);
    }

    [TestMethod]
    public async Task ReturnsNullWhenAllMethodsFail()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var http = new HttpClient(handler);
        var client = new AntigravityLanguageServerClient(http);

        var response = await client.FetchAsync(Candidate(), CancellationToken.None);

        Assert.IsNull(response);
    }

    [TestMethod]
    public async Task FallsBackToExtensionServerWithItsTokenWhenLoopbackFails()
    {
        const int mainPort = 42100;
        const int extPort = 53111;
        using var handler = new StubHandler(request =>
        {
            var onExtensionSummary = request.RequestUri!.Port == extPort &&
                request.RequestUri.AbsoluteUri.EndsWith("RetrieveUserQuotaSummary", StringComparison.Ordinal);
            return new HttpResponseMessage(onExtensionSummary ? HttpStatusCode.OK : HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"groups":[]}"""),
            };
        });
        using var http = new HttpClient(handler);
        var client = new AntigravityLanguageServerClient(http);
        var candidate = new AntigravityCandidate(
            Pid: 1, LoopbackPorts: [mainPort], CsrfToken: "main",
            ExtensionServerPort: extPort, ExtensionServerCsrfToken: "ext", IsCli: false);

        using var response = await client.FetchAsync(candidate, CancellationToken.None);

        Assert.IsNotNull(response);
        var extensionRequest = handler.Requests.Single(r => r.RequestUri!.Port == extPort);
        Assert.AreEqual("ext", extensionRequest.Headers.GetValues("X-Codeium-Csrf-Token").Single());
    }
}
