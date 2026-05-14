namespace CodexBar.Core.Refresh;

public interface IRefreshScheduler
{
    Task RefreshAllAsync(CancellationToken cancellationToken);
}
