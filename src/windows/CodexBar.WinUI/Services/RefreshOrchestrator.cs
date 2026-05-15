using System;
using System.Threading;
using System.Threading.Tasks;
using CodexBar.Core.Refresh;
using Microsoft.UI.Dispatching;

namespace CodexBar.WinUI.Services;

public sealed class RefreshOrchestrator : IDisposable
{
    private readonly IRefreshScheduler scheduler;
    private readonly Func<TimeSpan> intervalProvider;
    private readonly CancellationToken shutdownToken;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DispatcherQueueTimer? timer;

    public event EventHandler? Refreshed;

    public RefreshOrchestrator(
        IRefreshScheduler scheduler,
        Func<TimeSpan> intervalProvider,
        DispatcherQueue dispatcherQueue,
        CancellationToken shutdownToken)
    {
        this.scheduler = scheduler;
        this.intervalProvider = intervalProvider;
        this.dispatcherQueue = dispatcherQueue;
        this.shutdownToken = shutdownToken;
    }

    public void Start()
    {
        Stop();
        timer = dispatcherQueue.CreateTimer();
        timer.Interval = intervalProvider();
        timer.Tick += async (_, _) => await SafeTickAsync();
        timer.Start();
    }

    public void Stop()
    {
        timer?.Stop();
        timer = null;
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken)
    {
        if (!await gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            await scheduler.RefreshAllAsync(cancellationToken);
            Refreshed?.Invoke(this, EventArgs.Empty);
        }
        finally { gate.Release(); }
    }

    private async Task SafeTickAsync()
    {
        try { await RefreshNowAsync(shutdownToken); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshOrchestrator tick: {ex}");
        }
    }

    public void Dispose()
    {
        Stop();
        try { gate.Wait(TimeSpan.FromSeconds(5)); } catch { }
        gate.Dispose();
    }
}
