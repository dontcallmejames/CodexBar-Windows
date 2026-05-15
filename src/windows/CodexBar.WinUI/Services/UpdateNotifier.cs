using System;
using System.Threading;
using System.Threading.Tasks;
using CodexBar.Core.Updates;
using Microsoft.UI.Dispatching;

namespace CodexBar.WinUI.Services;

public sealed class UpdateNotifier : IDisposable
{
    private readonly IUpdateChecker checker;
    private readonly Action<UpdateCheckResult> onUpdateAvailable;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly CancellationToken shutdownToken;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DispatcherQueueTimer? timer;
    private string? lastNotifiedTag;

    public UpdateCheckResult? LatestResult { get; private set; }
    public event EventHandler? ResultChanged;

    public UpdateNotifier(
        IUpdateChecker checker,
        Action<UpdateCheckResult> onUpdateAvailable,
        DispatcherQueue dispatcherQueue,
        CancellationToken shutdownToken)
    {
        this.checker = checker;
        this.onUpdateAvailable = onUpdateAvailable;
        this.dispatcherQueue = dispatcherQueue;
        this.shutdownToken = shutdownToken;
    }

    public void Start(TimeSpan interval)
    {
        Stop();
        timer = dispatcherQueue.CreateTimer();
        timer.Interval = interval;
        timer.Tick += async (_, _) => await SafeTickAsync();
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

    private async Task SafeTickAsync()
    {
        try { await CheckNowAsync(shutdownToken); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateNotifier tick: {ex}");
        }
    }

    public void Dispose()
    {
        Stop();
        try { gate.Wait(TimeSpan.FromSeconds(5)); } catch { }
        gate.Dispose();
    }
}
