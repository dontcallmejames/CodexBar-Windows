using CodexBar.Core.Refresh;

namespace CodexBar.WinApp.Services;

public sealed class RefreshOrchestrator : IDisposable
{
    private readonly IRefreshScheduler scheduler;
    private readonly Func<TimeSpan> intervalProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private System.Windows.Threading.DispatcherTimer? timer;

    public event EventHandler? Refreshed;

    public RefreshOrchestrator(IRefreshScheduler scheduler, Func<TimeSpan> intervalProvider)
    {
        this.scheduler = scheduler;
        this.intervalProvider = intervalProvider;
    }

    public void Start()
    {
        Stop();
        timer = new System.Windows.Threading.DispatcherTimer { Interval = intervalProvider() };
        timer.Tick += async (_, _) => await RefreshNowAsync(default);
        timer.Start();
    }

    public void Stop()
    {
        timer?.Stop();
        timer = null;
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
        gate.Dispose();
    }
}
