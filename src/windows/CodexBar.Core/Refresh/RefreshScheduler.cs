using CodexBar.Core.Models;
using CodexBar.Core.Providers;

namespace CodexBar.Core.Refresh;

public sealed class RefreshScheduler : IRefreshScheduler
{
    private IReadOnlyList<IUsageProvider> providers;
    private readonly object providersLock = new();
    private readonly SnapshotStore store;
    private readonly ProviderRefreshStateRegistry registry;
    private readonly Func<DateTimeOffset> clock;

    public RefreshScheduler(
        IReadOnlyList<IUsageProvider> providers,
        SnapshotStore store,
        ProviderRefreshStateRegistry? registry = null,
        Func<DateTimeOffset>? clock = null)
    {
        this.providers = providers;
        this.store = store;
        this.clock = clock ?? (() => DateTimeOffset.Now);
        this.registry = registry ?? new ProviderRefreshStateRegistry(this.clock);
    }

    public ProviderRefreshStateRegistry Registry => registry;

    public void ReplaceProviders(IReadOnlyList<IUsageProvider> newProviders)
    {
        lock (providersLock) { providers = newProviders; }
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IUsageProvider> activeProviders;
        lock (providersLock) { activeProviders = providers; }
        var now = clock();
        foreach (var provider in activeProviders)
        {
            if (!registry.Get(provider.Provider).IsDue(now))
            {
                continue;
            }

            registry.RecordAttempt(provider.Provider);
            try
            {
                var snapshot = await provider.RefreshAsync(cancellationToken);
                store.Set(snapshot);
                if (snapshot.AuthState == AuthState.RequiresAuthentication)
                    registry.RecordFailure(provider.Provider, snapshot.ErrorMessage ?? "Authentication required.");
                else
                    registry.RecordSuccess(provider.Provider);
            }
            catch (RateLimitException rl)
            {
                registry.RecordFailure(provider.Provider, rl.Message, rl.RetryAfter);
                store.Set(FailedSnapshot(provider, rl.Message));
            }
            catch (AuthenticationRequiredException auth)
            {
                registry.RecordFailure(provider.Provider, auth.Message);
                store.Set(UsageSnapshot.RequiresAuthentication(provider.Provider, provider.Provider.ToString(), auth.Message));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                registry.RecordFailure(provider.Provider, error.Message);
                store.Set(FailedSnapshot(provider, error.Message));
            }
        }
    }

    private UsageSnapshot FailedSnapshot(IUsageProvider provider, string message)
    {
        var previous = store.Get(provider.Provider);
        return previous is null
            ? UsageSnapshot.MissingCredentials(provider.Provider, provider.Provider.ToString(), message)
            : previous with { IsStale = true, ErrorMessage = message };
    }
}
