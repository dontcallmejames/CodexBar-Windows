using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Core.Refresh;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class RefreshSchedulerBackoffTests
{
    [TestMethod]
    public async Task RateLimitedProvider_SkipsNextCycle_UntilBackoffElapses()
    {
        var clock = DateTimeOffset.Parse("2026-05-14T12:00:00Z");
        var current = clock;
        var registry = new ProviderRefreshStateRegistry(() => current);
        var store = new SnapshotStore();
        var provider = new FakeProvider(UsageProvider.Codex, throwRateLimited: true);
        var scheduler = new RefreshScheduler(new[] { (IUsageProvider)provider }, store, registry, () => current);

        await scheduler.RefreshAllAsync(default);
        Assert.AreEqual(1, provider.CallCount);

        current = clock.AddSeconds(10);
        await scheduler.RefreshAllAsync(default);
        Assert.AreEqual(1, provider.CallCount, "still inside backoff window");

        current = clock.AddMinutes(5);
        await scheduler.RefreshAllAsync(default);
        Assert.AreEqual(2, provider.CallCount, "past backoff, retried");
    }

    [TestMethod]
    public async Task SuccessfulProvider_AlwaysDue()
    {
        var clock = DateTimeOffset.Parse("2026-05-14T12:00:00Z");
        var registry = new ProviderRefreshStateRegistry(() => clock);
        var store = new SnapshotStore();
        var provider = new FakeProvider(UsageProvider.Codex, throwRateLimited: false);
        var scheduler = new RefreshScheduler(new[] { (IUsageProvider)provider }, store, registry, () => clock);

        await scheduler.RefreshAllAsync(default);
        await scheduler.RefreshAllAsync(default);

        Assert.AreEqual(2, provider.CallCount);
    }

    private sealed class FakeProvider : IUsageProvider
    {
        public FakeProvider(UsageProvider provider, bool throwRateLimited)
        { Provider = provider; this.throwRateLimited = throwRateLimited; }
        private readonly bool throwRateLimited;
        public UsageProvider Provider { get; }
        public int CallCount { get; private set; }
        public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            if (throwRateLimited)
            {
                throw new RateLimitException("rate limit", TimeSpan.FromMinutes(2));
            }
            return Task.FromResult(UsageSnapshot.MissingCredentials(Provider, Provider.ToString(), "ok"));
        }
    }
}
