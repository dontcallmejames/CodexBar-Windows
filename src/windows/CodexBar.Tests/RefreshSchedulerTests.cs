using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Core.Refresh;

namespace CodexBar.Tests;

[TestClass]
public sealed class RefreshSchedulerTests
{
    [TestMethod]
    public async Task RefreshAllKeepsLastGoodSnapshotWhenProviderFails()
    {
        var snapshot = new UsageSnapshot(
            UsageProvider.Codex,
            "Codex",
            DateTimeOffset.Now,
            Array.Empty<RateWindow>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "test",
            null,
            false);
        var provider = new FlakyProvider(snapshot);
        var store = new SnapshotStore();
        var scheduler = new RefreshScheduler(new IUsageProvider[] { provider }, store);

        await scheduler.RefreshAllAsync(CancellationToken.None);
        provider.ShouldThrow = true;
        await scheduler.RefreshAllAsync(CancellationToken.None);

        var stored = store.Get(UsageProvider.Codex);
        Assert.IsNotNull(stored);
        Assert.IsTrue(stored.IsStale);
        Assert.AreEqual("boom", stored.ErrorMessage);
        Assert.AreEqual(snapshot.UpdatedAt, stored.UpdatedAt);
    }

    private sealed class FlakyProvider : IUsageProvider
    {
        private readonly UsageSnapshot snapshot;

        public FlakyProvider(UsageSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public UsageProvider Provider => UsageProvider.Codex;
        public bool ShouldThrow { get; set; }

        public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(snapshot);
        }
    }
}
