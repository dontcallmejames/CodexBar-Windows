namespace CodexBar.Core.Models;

public sealed record ProviderRefreshResult(UsageProvider Provider, UsageSnapshot? Snapshot, string? ErrorMessage)
{
    public bool IsSuccess => Snapshot is not null && ErrorMessage is null;
}
