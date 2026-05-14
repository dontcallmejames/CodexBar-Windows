namespace CodexBar.WinApp.Services;

public sealed class UpdateNotifier : IDisposable
{
    private readonly IUpdateChecker checker;
    private readonly Action<UpdateCheckResult> onUpdateAvailable;
    private readonly SemaphoreSlim gate = new(1, 1);
    private System.Windows.Threading.DispatcherTimer? timer;
    private string? lastNotifiedTag;

    public UpdateCheckResult? LatestResult { get; private set; }
    public event EventHandler? ResultChanged;

    public UpdateNotifier(IUpdateChecker checker, Action<UpdateCheckResult> onUpdateAvailable)
    {
        this.checker = checker;
        this.onUpdateAvailable = onUpdateAvailable;
    }

    public void Start(TimeSpan interval)
    {
        Stop();
        timer = new System.Windows.Threading.DispatcherTimer { Interval = interval };
        timer.Tick += async (_, _) => await CheckNowAsync(default);
        timer.Start();
    }

    public void Stop()
    {
        timer?.Stop();
        timer = null;
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
        gate.Dispose();
    }
}
