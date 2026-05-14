using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using CodexBar.Tray;
using CodexBar.WinApp.Services;
using CodexBar.WinApp.ViewModels;
using CodexBar.WinApp.Views;
using System.Diagnostics;
using System.IO;

namespace CodexBar.WinApp;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource shutdown = new();
    private AppServices? services;
    private JsonSettingsStore? settingsStore;
    private TrayIconHost? tray;
    private WindowCoordinator? windowCoordinator;
    private IStartupRegistration? startupRegistration;
    private UpdateCheckResult? latestUpdateCheck;
    private System.Windows.Threading.DispatcherTimer? refreshTimer;
    private System.Windows.Threading.DispatcherTimer? updateCheckTimer;
    private bool isRefreshing;
    private bool isCheckingUpdates;
    private bool isShuttingDown;
    private string? lastNotifiedUpdateTag;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new WindowsAppPaths();
        settingsStore = new JsonSettingsStore(paths.SettingsFile);
        var settingsFileExists = File.Exists(paths.SettingsFile);
        var settings = await LoadSettingsOrDefaultAsync(paths, shutdown.Token);
        services = new AppServices(paths, settings);
        startupRegistration = new StartupRegistration(Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0]);
        ApplyStartupRegistration(settings);
        System.Windows.SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;

        windowCoordinator = new WindowCoordinator(services, settingsStore, Shutdown, RefreshNow, ApplySettings, shutdown.Token);
        tray = new TrayIconHost(windowCoordinator.ShowPopover, windowCoordinator.ShowSettings, Shutdown);
        tray.Update(new TrayDisplayModel("CodexBar", 0, true));

        try
        {
            await services.Scheduler.RefreshAllAsync(shutdown.Token);
            if (!isShuttingDown && !shutdown.IsCancellationRequested)
            {
                UpdateTrayFromSnapshots();
                windowCoordinator.UpdateTaskbarDock();
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }

        StartRefreshTimer(settings);
        StartUpdateCheckTimer(settings);
        if (ShouldShowFirstRunOnboarding(settingsFileExists))
        {
            windowCoordinator.ShowFirstRunOnboarding();
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        isShuttingDown = true;
        shutdown.Cancel();
        StopRefreshTimer();
        StopUpdateCheckTimer();
        windowCoordinator?.Dispose();
        System.Windows.SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        tray?.Dispose();
        services?.Dispose();
        shutdown.Dispose();
        base.OnExit(e);
    }

    private void StartRefreshTimer(AppSettings settings)
    {
        StopRefreshTimer();
        refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = WindowCoordinator.CalculateRefreshInterval(settings.RefreshMinutes)
        };
        refreshTimer.Tick += RefreshTimer_Tick;
        refreshTimer.Start();
    }

    private void StopRefreshTimer()
    {
        if (refreshTimer is null)
        {
            return;
        }

        refreshTimer.Stop();
        refreshTimer.Tick -= RefreshTimer_Tick;
        refreshTimer = null;
    }

    private void StartUpdateCheckTimer(AppSettings settings)
    {
        StopUpdateCheckTimer();
        var interval = WindowCoordinator.CalculateUpdateCheckInterval(settings.CheckForUpdatesAutomatically);
        if (interval is null)
        {
            return;
        }

        updateCheckTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = interval.Value
        };
        updateCheckTimer.Tick += UpdateCheckTimer_Tick;
        updateCheckTimer.Start();
        _ = CheckForUpdatesInBackgroundAsync();
    }

    private void StopUpdateCheckTimer()
    {
        if (updateCheckTimer is null)
        {
            return;
        }

        updateCheckTimer.Stop();
        updateCheckTimer.Tick -= UpdateCheckTimer_Tick;
        updateCheckTimer = null;
    }

    private async void UpdateCheckTimer_Tick(object? sender, EventArgs e)
    {
        await CheckForUpdatesInBackgroundAsync();
    }

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        if (services is null || !services.Settings.CheckForUpdatesAutomatically || isCheckingUpdates)
        {
            return;
        }

        isCheckingUpdates = true;
        var activeServices = services;
        try
        {
            var result = await activeServices.UpdateChecker.CheckAsync(shutdown.Token);
            if (!ReferenceEquals(services, activeServices) || isShuttingDown)
            {
                return;
            }

            latestUpdateCheck = result;
            if (windowCoordinator is not null)
            {
                windowCoordinator.LatestUpdateCheck = result;
                windowCoordinator.UpdateSettingsWindow();
            }

            if (result.UpdateAvailable && result.LatestTag is { Length: > 0 })
            {
                ShowUpdateAvailableNotification(result);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            latestUpdateCheck = UpdateCheckResult.Failed(error.Message);
            if (windowCoordinator is not null)
            {
                windowCoordinator.LatestUpdateCheck = latestUpdateCheck;
                windowCoordinator.UpdateSettingsWindow();
            }
        }
        finally
        {
            isCheckingUpdates = false;
        }
    }

    private void ShowUpdateAvailableNotification(UpdateCheckResult result)
    {
        if (tray is null || result.LatestTag == lastNotifiedUpdateTag)
        {
            return;
        }

        lastNotifiedUpdateTag = result.LatestTag;
        tray.ShowNotification(
            "CodexBar update available",
            $"{result.LatestTag} is available. Open Settings to download it.",
            System.Windows.Forms.ToolTipIcon.Info);
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (services is null || isRefreshing)
        {
            return;
        }

        isRefreshing = true;
        try
        {
            await RefreshServicesAsync(services);
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private async void RefreshNow()
    {
        if (services is null)
        {
            return;
        }

        await RefreshServicesAsync(services);
    }

    private async Task RefreshServicesAsync(AppServices activeServices)
    {
        try
        {
            await activeServices.Scheduler.RefreshAllAsync(shutdown.Token);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return;
        }

        if (!ReferenceEquals(services, activeServices) || isShuttingDown)
        {
            return;
        }

        UpdateTrayFromSnapshots();
        windowCoordinator?.OnSnapshotsChanged();
    }

    private void UpdateTrayFromSnapshots()
    {
        if (services is null || tray is null)
        {
            return;
        }

        tray.Update(BuildTrayDisplay(services.Store.All()));
    }

    private async void ApplySettings(AppSettings settings)
    {
        if (services is null)
        {
            return;
        }

        var paths = services.Paths;
        var previousStore = services.Store;
        services.Dispose();
        services = new AppServices(paths, settings);
        ApplyStartupRegistration(settings);
        StartRefreshTimer(settings);
        StartUpdateCheckTimer(settings);
        var activeServices = services;
        var snapshots = EnsureEnabledProviderSnapshots(
            FilterSnapshotsForSettings(previousStore.All(), settings),
            settings);
        foreach (var snapshot in snapshots)
        {
            services.Store.Set(snapshot);
        }

        UpdateTrayFromSnapshots();
        windowCoordinator?.OnSnapshotsChanged();
        await RefreshServicesAsync(activeServices);
    }

    private void ApplyStartupRegistration(AppSettings settings)
    {
        try
        {
            startupRegistration?.SetEnabled(settings.LaunchAtStartup);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(
                error.Message,
                "CodexBar Startup",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(System.Windows.SystemParameters.WorkArea))
        {
            windowCoordinator?.OnWorkAreaChanged();
        }
    }

    public static TrayDisplayModel BuildTrayDisplay(IReadOnlyList<UsageSnapshot> snapshots)
    {
        var primary = snapshots
            .SelectMany(snapshot => snapshot.Windows)
            .OrderByDescending(window => window.UsedPercent)
            .FirstOrDefault();
        var percent = primary?.UsedPercent ?? 0;
        var stale = snapshots.Count == 0 || snapshots.Any(snapshot => snapshot.IsStale);
        return new TrayDisplayModel("CodexBar", percent, stale);
    }

    public static IReadOnlyList<UsageSnapshot> FilterSnapshotsForSettings(
        IReadOnlyList<UsageSnapshot> snapshots,
        AppSettings settings) =>
        snapshots.Where(snapshot => snapshot.Provider switch
        {
            UsageProvider.Codex => settings.CodexEnabled,
            UsageProvider.Claude => settings.ClaudeEnabled,
            UsageProvider.Cursor => settings.CursorEnabled,
            UsageProvider.Gemini => settings.GeminiEnabled,
            _ => true
        }).ToArray();

    public static IReadOnlyList<UsageSnapshot> EnsureEnabledProviderSnapshots(
        IReadOnlyList<UsageSnapshot> snapshots,
        AppSettings settings)
    {
        var byProvider = snapshots.ToDictionary(snapshot => snapshot.Provider);
        foreach (var provider in EnabledProviders(settings))
        {
            if (!byProvider.ContainsKey(provider))
            {
                byProvider[provider] = UsageSnapshot.MissingCredentials(provider, ProviderDisplayName(provider), "Refreshing usage...");
            }
        }

        return byProvider.Values.OrderBy(snapshot => snapshot.Provider).ToArray();
    }

    private static IEnumerable<UsageProvider> EnabledProviders(AppSettings settings)
    {
        if (settings.CodexEnabled)
        {
            yield return UsageProvider.Codex;
        }

        if (settings.ClaudeEnabled)
        {
            yield return UsageProvider.Claude;
        }

        if (settings.CursorEnabled)
        {
            yield return UsageProvider.Cursor;
        }

        if (settings.GeminiEnabled)
        {
            yield return UsageProvider.Gemini;
        }
    }

    private static string ProviderDisplayName(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Codex => "Codex",
            UsageProvider.Claude => "Claude",
            UsageProvider.Cursor => "Cursor",
            UsageProvider.Gemini => "Gemini",
            _ => provider.ToString()
        };

    public static async Task<AppSettings> LoadSettingsOrDefaultAsync(
        WindowsAppPaths paths,
        CancellationToken cancellationToken)
    {
        try
        {
            var settingsStore = new JsonSettingsStore(paths.SettingsFile);
            return await settingsStore.LoadAsync(cancellationToken);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return AppSettings.Default;
        }
    }

    public static bool ShouldShowFirstRunOnboarding(bool settingsFileExists) =>
        !settingsFileExists;

    public static (double Left, double Top) CalculateSettingsPosition(
        double settingsWidth,
        double settingsHeight,
        double anchorLeft,
        double anchorTop,
        double anchorWidth,
        double anchorHeight,
        System.Windows.Rect workArea)
    {
        const double margin = 16;
        const double gap = 12;
        var rightCandidate = anchorLeft + anchorWidth + gap;
        var leftCandidate = anchorLeft - settingsWidth - gap;
        var maxLeft = workArea.Right - settingsWidth - margin;
        var left = rightCandidate <= maxLeft ? rightCandidate : leftCandidate;
        var anchorCenter = anchorTop + (anchorHeight / 2);
        var top = anchorCenter - (settingsHeight / 2);
        var maxTop = workArea.Bottom - settingsHeight - margin;

        return (
            Math.Clamp(left, workArea.Left + margin, maxLeft),
            Math.Clamp(top, workArea.Top + margin, maxTop));
    }
}
