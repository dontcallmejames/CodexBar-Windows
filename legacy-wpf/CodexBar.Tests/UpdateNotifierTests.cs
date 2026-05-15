using CodexBar.Core.Updates;
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
        var notifier = new UpdateNotifier(checker, r => notifications.Add(r), CancellationToken.None);

        await notifier.CheckNowAsync(default);
        await notifier.CheckNowAsync(default);

        Assert.AreEqual(1, notifications.Count);
    }

    [TestMethod]
    public async Task NewerTag_NotifiesAgain()
    {
        var checker = new FakeUpdateChecker();
        var notifications = new List<UpdateCheckResult>();
        var notifier = new UpdateNotifier(checker, r => notifications.Add(r), CancellationToken.None);

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
        var notifier = new UpdateNotifier(checker, _ => { }, CancellationToken.None);

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
        var notifier = new UpdateNotifier(checker, r => notifications.Add(r), CancellationToken.None);

        await notifier.CheckNowAsync(default);

        Assert.AreEqual(0, notifications.Count);
    }

    [TestMethod]
    public async Task Dispose_WhileCheckInFlight_DoesNotThrowOnSemaphore()
    {
        var checker = new FakeUpdateChecker
        {
            Result = UpdateCheckResult.UpToDate("v0.26"),
            Delay = TimeSpan.FromMilliseconds(200)
        };
        var notifier = new UpdateNotifier(checker, _ => { }, CancellationToken.None);
        var inflight = notifier.CheckNowAsync(CancellationToken.None);
        // Immediately dispose — should wait for the in-flight call and not throw
        notifier.Dispose();
        await inflight; // should complete without ObjectDisposedException
    }

    private sealed class FakeUpdateChecker : IUpdateChecker
    {
        public UpdateCheckResult? Result;
        public TimeSpan Delay;
        public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct)
        {
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
            return Result ?? UpdateCheckResult.UpToDate(null);
        }
    }
}
