using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Antigravity;

public sealed class AntigravityProvider : IUsageProvider
{
    private readonly AntigravityLanguageServerClient client;
    private readonly IAntigravityProcessLocator locator;

    public AntigravityProvider(HttpClient httpClient, IAntigravityProcessLocator locator)
    {
        client = new AntigravityLanguageServerClient(httpClient);
        this.locator = locator;
    }

    public UsageProvider Provider => UsageProvider.Antigravity;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        // FindCandidates() is a synchronous WMI + iphlpapi process scan. Run it on a worker thread
        // so it never blocks the caller (the UI thread, which drives the periodic refresh timer).
        var candidates = await Task.Run(locator.FindCandidates, cancellationToken);
        if (candidates.Count == 0)
        {
            return Missing("Antigravity isn't running.");
        }

        foreach (var candidate in candidates)
        {
            using var response = await client.FetchAsync(candidate, cancellationToken);
            if (response is null)
            {
                continue;
            }

            var snapshot = AntigravityUsageMapper.Map(response.Method, response.Document.RootElement, DateTimeOffset.Now);

            // RetrieveUserQuotaSummary carries quota but no identity. Backfill the plan tier and
            // account email from GetUserStatus so the card shows them.
            if (response.Method == "RetrieveUserQuotaSummary" &&
                (snapshot.Plan is null || snapshot.AccountEmail is null))
            {
                using var identity = await client.FetchUserStatusAsync(candidate, cancellationToken);
                if (identity is not null)
                {
                    var (plan, email) = AntigravityUsageMapper.ReadIdentity(identity.RootElement);
                    snapshot = snapshot with
                    {
                        Plan = snapshot.Plan ?? plan,
                        AccountEmail = snapshot.AccountEmail ?? email,
                    };
                }
            }

            return snapshot;
        }

        // Antigravity is running but no RPC returned quota — treat as transient and throw so
        // RefreshScheduler preserves the last-good snapshot (marked stale) instead of overwriting
        // it with an error card. Self-recovers on the next refresh.
        throw new AntigravityUnavailableException("Antigravity isn't available.");
    }

    private static UsageSnapshot Missing(string message) =>
        UsageSnapshot.MissingCredentials(UsageProvider.Antigravity, "Antigravity", message);
}

/// <summary>
/// Thrown when Antigravity is running but its language server returned no quota. Routed through
/// RefreshScheduler's generic failure path so the previous snapshot is kept as stale.
/// </summary>
public sealed class AntigravityUnavailableException : Exception
{
    public AntigravityUnavailableException(string message) : base(message)
    {
    }
}
