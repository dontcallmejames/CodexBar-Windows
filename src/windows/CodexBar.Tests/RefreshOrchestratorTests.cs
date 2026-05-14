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
        var orchestrator = new RefreshOrchestrator(fake, () => TimeSpan.FromMinutes(5));
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
        var orchestrator = new RefreshOrchestrator(fake, () => TimeSpan.FromMinutes(5));

        var t1 = orchestrator.RefreshNowAsync(default);
        var t2 = orchestrator.RefreshNowAsync(default);
        await Task.WhenAll(t1, t2);

        Assert.AreEqual(1, fake.Calls, "second call should be coalesced");
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
