using System.Collections.Concurrent;
using CodexBar.Core.Models;

namespace CodexBar.Core.Refresh;

public sealed record ProviderRefreshState(
    DateTimeOffset? LastSuccess,
    DateTimeOffset? LastAttempt,
    int ConsecutiveFailures,
    DateTimeOffset? NextAllowedAt,
    string? LastErrorMessage)
{
    public static ProviderRefreshState Empty { get; } =
        new(null, null, 0, null, null);

    public bool IsDue(DateTimeOffset now) =>
        NextAllowedAt is null || now >= NextAllowedAt;
}

public sealed class ProviderRefreshStateRegistry
{
    private readonly Func<DateTimeOffset> clock;
    private readonly ConcurrentDictionary<UsageProvider, ProviderRefreshState> states = new();

    public ProviderRefreshStateRegistry(Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.Now);
    }

    public ProviderRefreshState Get(UsageProvider provider) =>
        states.TryGetValue(provider, out var state) ? state : ProviderRefreshState.Empty;

    public void RecordAttempt(UsageProvider provider)
    {
        var now = clock();
        states.AddOrUpdate(provider,
            _ => ProviderRefreshState.Empty with { LastAttempt = now },
            (_, prev) => prev with { LastAttempt = now });
    }

    public void RecordSuccess(UsageProvider provider)
    {
        var now = clock();
        states[provider] = new ProviderRefreshState(
            LastSuccess: now,
            LastAttempt: now,
            ConsecutiveFailures: 0,
            NextAllowedAt: null,
            LastErrorMessage: null);
    }

    public void RecordFailure(UsageProvider provider, string? message = null, TimeSpan? retryAfter = null)
    {
        var now = clock();
        states.AddOrUpdate(provider,
            _ => Build(now, 1, message, retryAfter),
            (_, prev) => Build(now, prev.ConsecutiveFailures + 1, message, retryAfter, prev.LastSuccess));
    }

    private static ProviderRefreshState Build(
        DateTimeOffset now,
        int failures,
        string? message,
        TimeSpan? retryAfter,
        DateTimeOffset? lastSuccess = null) =>
        new(lastSuccess, now, failures, now + AdaptiveBackoff.Delay(failures, retryAfter), message);
}
