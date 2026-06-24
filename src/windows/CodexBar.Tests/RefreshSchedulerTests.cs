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

    [TestMethod]
    public async Task ThrownAuthenticationRequiredStoresEmptyAuthSnapshotAndEngagesBackoff()
    {
        var goodSnapshot = new UsageSnapshot(
            UsageProvider.Codex, "Codex", DateTimeOffset.Now,
            new[] { new RateWindow("session", "Session", 50, null, null) },
            null, null, null, null, null, null, null, "test", null, false);
        var provider = new ThrowingAuthProvider(goodSnapshot);
        var store = new SnapshotStore();
        var registry = new ProviderRefreshStateRegistry();
        var scheduler = new RefreshScheduler(new IUsageProvider[] { provider }, store, registry);

        // First refresh succeeds and seeds a last-good snapshot with real numbers.
        await scheduler.RefreshAllAsync(CancellationToken.None);
        Assert.AreEqual(1, store.Get(UsageProvider.Codex)!.Windows.Count);

        // Now the credential dies: the provider throws AuthenticationRequiredException.
        provider.ShouldThrow = true;
        await scheduler.RefreshAllAsync(CancellationToken.None);

        var stored = store.Get(UsageProvider.Codex);
        Assert.IsNotNull(stored);
        Assert.AreEqual(AuthState.RequiresAuthentication, stored.AuthState);
        Assert.AreEqual(0, stored.Windows.Count, "auth snapshot must NOT carry over stale numbers");
        StringAssert.Contains(stored.ErrorMessage!, "codex login");

        // Backoff engaged: a failure was recorded and the provider is no longer immediately due.
        var state = registry.Get(UsageProvider.Codex);
        Assert.AreEqual(1, state.ConsecutiveFailures);
        Assert.IsFalse(state.IsDue(DateTimeOffset.Now));
    }

    [TestMethod]
    public async Task ReturnedAuthenticationRequiredSnapshotRecordsFailureNotSuccess()
    {
        var authSnapshot = UsageSnapshot.RequiresAuthentication(UsageProvider.Cursor, "Cursor", "Re-paste your cookie.");
        var provider = new StaticProvider(UsageProvider.Cursor, authSnapshot);
        var store = new SnapshotStore();
        var registry = new ProviderRefreshStateRegistry();
        var scheduler = new RefreshScheduler(new IUsageProvider[] { provider }, store, registry);

        await scheduler.RefreshAllAsync(CancellationToken.None);

        var stored = store.Get(UsageProvider.Cursor);
        Assert.IsNotNull(stored);
        Assert.AreEqual(AuthState.RequiresAuthentication, stored.AuthState);

        var state = registry.Get(UsageProvider.Cursor);
        Assert.AreEqual(1, state.ConsecutiveFailures, "a returned auth snapshot must debounce as a failure, not a success");
        Assert.IsNull(state.LastSuccess);
    }

    [TestMethod]
    public async Task MissingCredentialsSnapshotStillRecordsSuccess()
    {
        var missing = UsageSnapshot.MissingCredentials(UsageProvider.Gemini, "Gemini", "Not configured.");
        var provider = new StaticProvider(UsageProvider.Gemini, missing);
        var store = new SnapshotStore();
        var registry = new ProviderRefreshStateRegistry();
        var scheduler = new RefreshScheduler(new IUsageProvider[] { provider }, store, registry);

        await scheduler.RefreshAllAsync(CancellationToken.None);

        var state = registry.Get(UsageProvider.Gemini);
        Assert.AreEqual(0, state.ConsecutiveFailures, "not-configured must keep cheaply re-polling");
        Assert.IsNotNull(state.LastSuccess);
    }

    private sealed class ThrowingAuthProvider : IUsageProvider
    {
        private readonly UsageSnapshot snapshot;

        public ThrowingAuthProvider(UsageSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public UsageProvider Provider => UsageProvider.Codex;
        public bool ShouldThrow { get; set; }

        public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
        {
            if (ShouldThrow)
            {
                throw new AuthenticationRequiredException(
                    "Your Codex sign-in expired. Run `codex login` in a terminal to reconnect.");
            }

            return Task.FromResult(snapshot);
        }
    }

    private sealed class StaticProvider : IUsageProvider
    {
        private readonly UsageSnapshot snapshot;

        public StaticProvider(UsageProvider provider, UsageSnapshot snapshot)
        {
            Provider = provider;
            this.snapshot = snapshot;
        }

        public UsageProvider Provider { get; }

        public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
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
