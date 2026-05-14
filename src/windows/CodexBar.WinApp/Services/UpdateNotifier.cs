namespace CodexBar.WinApp.Services;

public sealed class UpdateNotifier : IDisposable
{
    private readonly IUpdateChecker checker;
    private readonly Action<UpdateCheckResult> onUpdateAvailable;
    private readonly CancellationToken shutdownToken;
    private readonly SemaphoreSlim gate = new(1, 1);
    private System.Windows.Threading.DispatcherTimer? timer;
    private string? lastNotifiedTag;

    public UpdateCheckResult? LatestResult { get; private set; }
    public event EventHandler? ResultChanged;

    public UpdateNotifier(IUpdateChecker checker, Action<UpdateCheckResult> onUpdateAvailable, CancellationToken shutdownToken)
    {
        this.checker = checker;
        this.onUpdateAvailable = onUpdateAvailable;
        this.shutdownToken = shutdownToken;
    }

    public void Start(TimeSpan interval)
    {
        Stop();
        timer = new System.Windows.Threading.DispatcherTimer { Interval = interval };
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
            await CheckNowAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UpdateNotifier] Tick error: {ex}");
        }
    }

    public async Task CheckNowAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var result = await checker.CheckAsync(cancellationToken);
            LatestResult = result;
            ResultChanged?.Invoke(this, EventArgs.Empty);
            if (result.UpdateAvailable && result.LatestTag != lastNotifiedTag)
            {
                lastNotifiedTag = result.LatestTag;
                onUpdateAvailable(result);
            }
        }
        finally { gate.Release(); }
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
