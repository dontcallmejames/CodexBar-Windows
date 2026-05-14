using CodexBar.Core.Refresh;
using CodexBar.WinApp.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class RefreshOrchestratorTests
{
    [TestMethod]
    public async Task ManualRefresh_InvokesScheduler_AndRaisesEvent()
    {
        var fake = new FakeScheduler();
        var orchestrator = new RefreshOrchestrator(fake, () => TimeSpan.FromMinutes(5), CancellationToken.None);
        var raised = 0;
        orchestrator.Refreshed += (_, _) => raised++;

        await orchestrator.RefreshNowAsync(default);

        Assert.AreEqual(1, fake.Calls);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public async Task ConcurrentRefresh_Coalesces()
    {
        var fake = new FakeScheduler { Delay = TimeSpan.FromMilliseconds(50) };
        var orchestrator = new RefreshOrchestrator(fake, () => TimeSpan.FromMinutes(5), CancellationToken.None);

        var t1 = orchestrator.RefreshNowAsync(default);
        var t2 = orchestrator.RefreshNowAsync(default);
        await Task.WhenAll(t1, t2);

        Assert.AreEqual(1, fake.Calls, "second call should be coalesced");
    }

    [TestMethod]
    public async Task Dispose_WhileRefreshInFlight_DoesNotThrowOnSemaphore()
    {
        var fake = new FakeScheduler { Delay = TimeSpan.FromMilliseconds(200) };
        var orchestrator = new RefreshOrchestrator(fake, () => TimeSpan.FromMinutes(5), CancellationToken.None);
        var inflight = orchestrator.RefreshNowAsync(CancellationToken.None);
        // Immediately dispose — should wait for the in-flight call and not throw
        orchestrator.Dispose();
        await inflight; // should complete without ObjectDisposedException
    }

    private sealed class FakeScheduler : IRefreshScheduler
    {
        public int Calls;
        public TimeSpan Delay;
        public async Task RefreshAllAsync(CancellationToken ct)
        {
            Calls++;
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
        }
    }
}
