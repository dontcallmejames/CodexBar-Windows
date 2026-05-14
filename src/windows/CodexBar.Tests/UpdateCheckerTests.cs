using CodexBar.WinApp;
using CodexBar.WinApp.Services;
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
        [
          {
            "tag_name": "v0.25.0-preview.4",
            "html_url": "https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.4",
            "draft": true,
            "prerelease": true
          },
          {
            "tag_name": "v0.25.0-preview.3",
            "html_url": "https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.3",
            "draft": false,
            "prerelease": true
          }
        ]
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
        [
          {
            "tag_name": "v0.25.0-preview.2",
            "html_url": "https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.2",
            "draft": false,
            "prerelease": true
          }
        ]
        """));
        var checker = new GitHubUpdateChecker(httpClient, AppVersionInfo.FromMarketingVersion("0.25", "2"));

        var result = await checker.CheckAsync(CancellationToken.None);

        Assert.IsFalse(result.UpdateAvailable);
        Assert.AreEqual("v0.25.0-preview.2", result.LatestTag);
        Assert.AreEqual("You're on the latest release.", result.StatusText);
    }

    [TestMethod]
    public async Task ReportsFriendlyFailureForGithubErrors()
    {
        using var httpClient = new HttpClient(new StatusHandler(HttpStatusCode.NotFound));
        var checker = new GitHubUpdateChecker(httpClient, AppVersionInfo.FromMarketingVersion("0.25", "2"));

        var result = await checker.CheckAsync(CancellationToken.None);

        Assert.IsFalse(result.UpdateAvailable);
        Assert.AreEqual("Update check failed. Open Releases to check manually.", result.StatusText);
        StringAssert.Contains(result.ErrorMessage, "404");
    }

    // --- UpdateNotifier immediate-check behaviour (mirrors ApplySettings fix) ---

    [TestMethod]
    public async Task UpdateNotifier_CheckNowAsync_FiresImmediatelyAfterStart()
    {
        // Simulates the ApplySettings path: Start(interval) then CheckNowAsync.
        // Verifies the checker is called and LatestResult is populated.
        int checkCount = 0;
        var fakeChecker = new FakeUpdateChecker(() =>
        {
            checkCount++;
            return Task.FromResult(new UpdateCheckResult(
                UpdateAvailable: false,
                LatestTag: "v1.0.0",
                ReleaseUri: null,
                StatusText: "Up to date",
                ErrorMessage: null));
        });

        using var notifier = new UpdateNotifier(
            fakeChecker,
            _ => { },
            CancellationToken.None);

        // Start a 24h timer (won't tick during the test) then fire an immediate check
        notifier.Start(TimeSpan.FromHours(24));
        await notifier.CheckNowAsync(CancellationToken.None);

        Assert.AreEqual(1, checkCount, "Checker should have been called exactly once by the immediate CheckNowAsync");
        Assert.IsNotNull(notifier.LatestResult);
        Assert.IsFalse(notifier.LatestResult!.UpdateAvailable);
    }

    [TestMethod]
    public async Task UpdateNotifier_CheckNowAsync_CanBeCalledBeforeStart()
    {
        // Verifies CheckNowAsync works even when Start hasn't been called yet
        // (edge case: toggling setting on from disabled state).
        int checkCount = 0;
        var fakeChecker = new FakeUpdateChecker(() =>
        {
            checkCount++;
            return Task.FromResult(new UpdateCheckResult(
                UpdateAvailable: false,
                LatestTag: "v1.0.0",
                ReleaseUri: null,
                StatusText: "Up to date",
                ErrorMessage: null));
        });

        using var notifier = new UpdateNotifier(
            fakeChecker,
            _ => { },
            CancellationToken.None);

        await notifier.CheckNowAsync(CancellationToken.None);

        Assert.AreEqual(1, checkCount);
    }

    private sealed class FakeUpdateChecker : IUpdateChecker
    {
        private readonly Func<Task<UpdateCheckResult>> check;
        public FakeUpdateChecker(Func<Task<UpdateCheckResult>> check) => this.check = check;
        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken) => check();
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
            Assert.AreEqual(new Uri("https://api.github.com/repos/dontcallmejames/CodexBar-Windows/releases?per_page=20"), request.RequestUri);
            Assert.IsTrue(request.Headers.UserAgent.Any());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StatusHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;

        public StatusHandler(HttpStatusCode statusCode)
        {
            this.statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
