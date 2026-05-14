using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using CodexBar.Tray;
using System.IO;

namespace CodexBar.WinApp.Services;

/// <summary>
/// Owns the refresh/update orchestrators, tray controller, window coordinator, and startup
/// registration. App.xaml.cs constructs this and calls StartAsync.
/// </summary>
public sealed class AppShellController : IDisposable
{
    private readonly CancellationTokenSource shutdown = new();
    private readonly JsonSettingsStore settingsStore;
    private readonly string settingsFilePath;
    private readonly IStartupRegistration startupRegistration;
    private readonly TrayIconHost trayIconHost;
    private AppServices services;
    private RefreshOrchestrator refreshOrchestrator;
    private readonly UpdateNotifier updateNotifier;
    private readonly TrayController trayController;
    private readonly WindowCoordinator windowCoordinator;
    private bool started;

    public AppShellController(
        AppServices services,
        JsonSettingsStore settingsStore,
        string settingsFilePath,
        IStartupRegistration startupRegistration)
    {
        this.services = services;
        this.settingsStore = settingsStore;
        this.settingsFilePath = settingsFilePath;
        this.startupRegistration = startupRegistration;

        refreshOrchestrator = BuildRefreshOrchestrator();

        updateNotifier = new UpdateNotifier(
            services.UpdateChecker,
            ShowUpdateAvailableNotification,
            shutdown.Token);

        windowCoordinator = new WindowCoordinator(
            services,
            settingsStore,
            quit: () => System.Windows.Application.Current.Shutdown(),
            refreshNow: RefreshNow,
            applySettings: ApplySettings,
            shutdownToken: shutdown.Token);

        trayIconHost = new TrayIconHost(
            onLeftClick: windowCoordinator.ShowPopover,
            onSettingsClick: windowCoordinator.ShowSettings,
            onQuitClick: () => System.Windows.Application.Current.Shutdown());

        trayController = new TrayController(trayIconHost);
        trayController.Apply(Array.Empty<UsageSnapshot>(), services.Settings.ShowUsageAsUsed);

        refreshOrchestrator.Refreshed += OnRefreshed;
        updateNotifier.ResultChanged += OnUpdateResultChanged;
        System.Windows.SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
    }

    private RefreshOrchestrator BuildRefreshOrchestrator() =>
        new RefreshOrchestrator(
            services.Scheduler,
            () => WindowCoordinator.CalculateRefreshInterval(services.Settings.RefreshMinutes),
            shutdown.Token);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (started) return;
        started = true;

        ApplyStartupRegistration(services.Settings);

        try
        {
            await services.Scheduler.RefreshAllAsync(shutdown.Token);
            if (!shutdown.IsCancellationRequested)
            {
                ApplySnapshotsToTray();
                windowCoordinator.UpdateTaskbarDock();
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }

        var settings = services.Settings;
        refreshOrchestrator.Start();

        var updateInterval = WindowCoordinator.CalculateUpdateCheckInterval(settings.CheckForUpdatesAutomatically);
        if (updateInterval is not null)
        {
            updateNotifier.Start(updateInterval.Value);
            _ = updateNotifier.CheckNowAsync(shutdown.Token);
        }

        if (ShouldShowFirstRunOnboarding(File.Exists(settingsFilePath)))
        {
            windowCoordinator.ShowFirstRunOnboarding();
        }
    }

    private void OnRefreshed(object? sender, EventArgs e)
    {
        ApplySnapshotsToTray();
        windowCoordinator.OnSnapshotsChanged();
    }

    private void OnUpdateResultChanged(object? sender, EventArgs e)
    {
        if (updateNotifier.LatestResult is { } result)
        {
            windowCoordinator.LatestUpdateCheck = result;
            windowCoordinator.UpdateSettingsWindow();
        }
    }

    private void ApplySnapshotsToTray()
    {
        trayController.Apply(services.Store.All(), services.Settings.ShowUsageAsUsed);
    }

    private void ShowUpdateAvailableNotification(UpdateCheckResult result)
    {
        trayIconHost.ShowNotification(
            "CodexBar update available",
            $"{result.LatestTag} is available. Open Settings to download it.",
            System.Windows.Forms.ToolTipIcon.Info);
    }

    private void ApplySettings(AppSettings settings)
    {
        var paths = services.Paths;
        var previousStore = services.Store;

        refreshOrchestrator.Stop();
        refreshOrchestrator.Refreshed -= OnRefreshed;
        refreshOrchestrator.Dispose();

        services.Dispose();
        services = new AppServices(paths, settings);

        refreshOrchestrator = BuildRefreshOrchestrator();
        refreshOrchestrator.Refreshed += OnRefreshed;

        ApplyStartupRegistration(settings);

        var snapshots = EnsureEnabledProviderSnapshots(
            FilterSnapshotsForSettings(previousStore.All(), settings),
            settings);
        foreach (var snapshot in snapshots)
        {
            services.Store.Set(snapshot);
        }

        ApplySnapshotsToTray();
        windowCoordinator.OnSnapshotsChanged();

        refreshOrchestrator.Start();

        var updateInterval = WindowCoordinator.CalculateUpdateCheckInterval(settings.CheckForUpdatesAutomatically);
        updateNotifier.Stop();
        if (updateInterval is not null)
        {
            updateNotifier.Start(updateInterval.Value);
        }

        _ = RefreshServicesAsync(services);
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

        if (!ReferenceEquals(services, activeServices)) return;

        ApplySnapshotsToTray();
        windowCoordinator.OnSnapshotsChanged();
    }

    private async void RefreshNow()
    {
        await RefreshServicesAsync(services);
    }

    private void ApplyStartupRegistration(AppSettings settings)
    {
        try
        {
            startupRegistration.SetEnabled(settings.LaunchAtStartup);
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
            windowCoordinator.OnWorkAreaChanged();
        }
    }

    // --- Static helpers (moved from App.xaml.cs) ---

    public static bool ShouldShowFirstRunOnboarding(bool settingsFileExists) =>
        !settingsFileExists;

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

    public static async Task<AppSettings> LoadSettingsOrDefaultAsync(
        WindowsAppPaths paths,
        CancellationToken cancellationToken)
    {
        try
        {
            var store = new JsonSettingsStore(paths.SettingsFile);
            return await store.LoadAsync(cancellationToken);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return AppSettings.Default;
        }
    }

    private static IEnumerable<UsageProvider> EnabledProviders(AppSettings settings)
    {
        if (settings.CodexEnabled) yield return UsageProvider.Codex;
        if (settings.ClaudeEnabled) yield return UsageProvider.Claude;
        if (settings.CursorEnabled) yield return UsageProvider.Cursor;
        if (settings.GeminiEnabled) yield return UsageProvider.Gemini;
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

    public void Dispose()
    {
        refreshOrchestrator.Refreshed -= OnRefreshed;
        updateNotifier.ResultChanged -= OnUpdateResultChanged;
        System.Windows.SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        shutdown.Cancel();
        refreshOrchestrator.Dispose();
        updateNotifier.Dispose();
        windowCoordinator.Dispose();
        trayController.Dispose();
        services.Dispose();
        shutdown.Dispose();
    }
}
