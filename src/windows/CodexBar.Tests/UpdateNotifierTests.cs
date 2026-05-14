using CodexBar.WinApp;
using CodexBar.WinApp.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public sealed class UpdateNotifierTests
{
    [TestMethod]
    public async Task NotifiesOncePerTag()
    {
        var checker = new FakeUpdateChecker
        {
            Result = UpdateCheckResult.Available("v0.26", new Uri("https://example.com/v0.26"))
        };
        var notifications = new List<UpdateCheckResult>();
        var notifier = new UpdateNotifier(checker, r => notifications.Add(r));

        await notifier.CheckNowAsync(default);
        await notifier.CheckNowAsync(default);

        Assert.AreEqual(1, notifications.Count);
    }

    [TestMethod]
    public async Task NewerTag_NotifiesAgain()
    {
        var checker = new FakeUpdateChecker();
        var notifications = new List<UpdateCheckResult>();
        var notifier = new UpdateNotifier(checker, r => notifications.Add(r));

        checker.Result = UpdateCheckResult.Available("v0.26", new Uri("https://example.com/v0.26"));
        await notifier.CheckNowAsync(default);
        checker.Result = UpdateCheckResult.Available("v0.27", new Uri("https://example.com/v0.27"));
        await notifier.CheckNowAsync(default);

        Assert.AreEqual(2, notifications.Count);
    }

    [TestMethod]
    public async Task LatestResult_UpdatedAfterCheck()
    {
        var expected = UpdateCheckResult.Available("v0.26", new Uri("https://example.com/v0.26"));
        var checker = new FakeUpdateChecker { Result = expected };
        var notifier = new UpdateNotifier(checker, _ => { });

        await notifier.CheckNowAsync(default);

        Assert.AreEqual(expected, notifier.LatestResult);
    }

    [TestMethod]
    public async Task NoNotification_WhenNotUpdateAvailable()
    {
        var checker = new FakeUpdateChecker
        {
            Result = UpdateCheckResult.UpToDate("v0.26")
        };
        var notifications = new List<UpdateCheckResult>();
        var notifier = new UpdateNotifier(checker, r => notifications.Add(r));

        await notifier.CheckNowAsync(default);

        Assert.AreEqual(0, notifications.Count);
    }

    private sealed class FakeUpdateChecker : IUpdateChecker
    {
        public UpdateCheckResult? Result;
        public Task<UpdateCheckResult> CheckAsync(CancellationToken ct) =>
            Task.FromResult(Result ?? UpdateCheckResult.UpToDate(null));
    }
}
