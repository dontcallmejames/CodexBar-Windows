using CodexBar.Core.Refresh;

namespace CodexBar.WinApp.Services;

public sealed class RefreshOrchestrator : IDisposable
{
    private readonly IRefreshScheduler scheduler;
    private readonly Func<TimeSpan> intervalProvider;
    private readonly CancellationToken shutdownToken;
    private readonly SemaphoreSlim gate = new(1, 1);
    private System.Windows.Threading.DispatcherTimer? timer;

    public event EventHandler? Refreshed;

    public RefreshOrchestrator(IRefreshScheduler scheduler, Func<TimeSpan> intervalProvider, CancellationToken shutdownToken)
    {
        this.scheduler = scheduler;
        this.intervalProvider = intervalProvider;
        this.shutdownToken = shutdownToken;
    }

    public void Start()
    {
        Stop();
        timer = new System.Windows.Threading.DispatcherTimer { Interval = intervalProvider() };
        timer.Tick += async (_, _) => await SafeTickAsync(shutdownToken);
        timer.Start();
    }

    public void Stop()
    {
        timer?.Stop();
        timer = null;
    }

    private async Task SafeTickAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshNowAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RefreshOrchestrator] Tick error: {ex}");
        }
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return;
        }
        try
        {
            await scheduler.RefreshAllAsync(cancellationToken);
            Refreshed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        Stop();
        try
        {
            if (gate.Wait(TimeSpan.FromSeconds(5)))
            {
                // we hold the gate now; in-flight work has completed
            }
        }
        catch (ObjectDisposedException) { }
        gate.Dispose();
    }
}
