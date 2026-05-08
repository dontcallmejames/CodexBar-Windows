using CodexBar.WinApp;
using System.Net;
using System.Text;

namespace CodexBar.Tests;

[TestClass]
public sealed class UpdateCheckerTests
{
    [TestMethod]
    public async Task ParsesLatestGithubReleaseAndReportsAvailableUpdate()
    {
        using var httpClient = new HttpClient(new StaticJsonHandler("""
        {
          "tag_name": "v0.25.0-preview.3",
          "html_url": "https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.3",
          "prerelease": true
        }
        """));
        var checker = new GitHubUpdateChecker(httpClient, AppVersionInfo.FromMarketingVersion("0.25", "2"));

        var result = await checker.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual("v0.25.0-preview.3", result.LatestTag);
        Assert.AreEqual(new Uri("https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.3"), result.ReleaseUri);
    }

    [TestMethod]
    public async Task ReportsUpToDateWhenCurrentTagMatchesLatest()
    {
        using var httpClient = new HttpClient(new StaticJsonHandler("""
        {
          "tag_name": "v0.25.0-preview.2",
          "html_url": "https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.2",
          "prerelease": true
        }
        """));
        var checker = new GitHubUpdateChecker(httpClient, AppVersionInfo.FromMarketingVersion("0.25", "2"));

        var result = await checker.CheckAsync(CancellationToken.None);

        Assert.IsFalse(result.UpdateAvailable);
        Assert.AreEqual("v0.25.0-preview.2", result.LatestTag);
    }

    private sealed class StaticJsonHandler : HttpMessageHandler
    {
        private readonly string json;

        public StaticJsonHandler(string json)
        {
            this.json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual(new Uri("https://api.github.com/repos/dontcallmejames/CodexBar-Windows/releases/latest"), request.RequestUri);
            Assert.IsTrue(request.Headers.UserAgent.Any());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
