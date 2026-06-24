namespace CodexBar.Core.Models;

public enum AuthState
{
    None,
    RequiresAuthentication
}

public sealed record UsageSnapshot(
    UsageProvider Provider,
    string DisplayName,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RateWindow> Windows,
    string? AccountEmail,
    string? Plan,
    decimal? CreditsRemaining,
    decimal? TodayCostUsd,
    long? TodayTokens,
    decimal? Last30DaysCostUsd,
    long? Last30DaysTokens,
    string SourceLabel,
    string? ErrorMessage,
    bool IsStale)
{
    public AuthState AuthState { get; init; } = AuthState.None;

    public static UsageSnapshot MissingCredentials(UsageProvider provider, string displayName, string message) =>
        new(provider, displayName, DateTimeOffset.Now, Array.Empty<RateWindow>(), null, null, null, null, null, null, null, "none", message, true);

    public static UsageSnapshot RequiresAuthentication(UsageProvider provider, string displayName, string message) =>
        new(provider, displayName, DateTimeOffset.Now, Array.Empty<RateWindow>(), null, null, null, null, null, null, null, "none", message, true) { AuthState = AuthState.RequiresAuthentication };
}
