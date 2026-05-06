using CodexBar.Core.Models;

namespace CodexBar.Core.Providers;

public interface IUsageProvider
{
    UsageProvider Provider { get; }
    Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken);
}
