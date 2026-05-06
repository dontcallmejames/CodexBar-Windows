using CodexBar.Core.Models;
using CodexBar.Core.Providers;

namespace CodexBar.Core.Refresh;

public sealed class RefreshScheduler
{
    private readonly IReadOnlyList<IUsageProvider> providers;
    private readonly SnapshotStore store;

    public RefreshScheduler(IReadOnlyList<IUsageProvider> providers, SnapshotStore store)
    {
        this.providers = providers;
        this.store = store;
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in providers)
        {
            try
            {
                store.Set(await provider.RefreshAsync(cancellationToken));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
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
