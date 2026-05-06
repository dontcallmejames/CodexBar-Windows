using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using CodexBar.Tray;
using CodexBar.WinApp.ViewModels;
using CodexBar.WinApp.Views;

namespace CodexBar.WinApp;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource shutdown = new();
    private AppServices? services;
    private JsonSettingsStore? settingsStore;
    private TrayIconHost? tray;
    private PopoverWindow? popover;
    private DockedOverviewWindow? dockedOverview;
    private SettingsWindow? settingsWindow;
    private bool isShuttingDown;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new WindowsAppPaths();
        settingsStore = new JsonSettingsStore(paths.SettingsFile);
        var settings = await LoadSettingsOrDefaultAsync(paths, shutdown.Token);
        services = new AppServices(paths, settings);

        tray = new TrayIconHost(ShowPopover, ShowSettings, Shutdown);
        tray.Update(new TrayDisplayModel("CodexBar", 0, true));

        try
        {
            await services.Scheduler.RefreshAllAsync(shutdown.Token);
            if (!isShuttingDown && !shutdown.IsCancellationRequested)
            {
                UpdateTrayFromSnapshots();
                UpdateDockedOverview();
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        isShuttingDown = true;
        shutdown.Cancel();
        settingsWindow?.Close();
        dockedOverview?.Close();
        popover?.Close();
        tray?.Dispose();
        services?.Dispose();
        shutdown.Dispose();
        base.OnExit(e);
    }

    private void ShowPopover()
    {
        if (popover?.IsVisible == true)
        {
            popover.Close();
            popover = null;
            return;
        }

        if (services is null)
        {
            return;
        }

        var viewModel = new PopoverViewModel(
            services.Store.All(),
            UsageProvider.Codex,
            services.Settings.ShowUsageAsUsed,
            openDashboard: ShowUsageDashboard,
            openSettings: ShowSettings,
            showAbout: ShowAbout,
            quit: Shutdown);
        popover = new PopoverWindow(viewModel);
        popover.Closed += (_, _) => popover = null;
        popover.Show();
        popover.Activate();
    }

    private static void ShowUsageDashboard()
    {
        const string message = "Usage dashboard actions will open provider dashboards after Task 8 wires live providers.";
        System.Windows.MessageBox.Show(message, "CodexBar");
    }

    private void ShowSettings()
    {
        if (services is null || settingsStore is null)
        {
            return;
        }

        if (settingsWindow?.IsVisible == true)
        {
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new SettingsWindow(services.Settings, settingsStore);
        settingsWindow.SettingsSaved += (_, settings) => ApplySettings(settings);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private static void ShowAbout()
    {
        const string message = "CodexBar for Windows";
        System.Windows.MessageBox.Show(message, "About CodexBar");
    }

    private void ApplySettings(AppSettings settings)
    {
        if (services is null)
        {
            return;
        }

        var paths = services.Paths;
        var previousStore = services.Store;
        services.Dispose();
        services = new AppServices(paths, settings);
        foreach (var snapshot in FilterSnapshotsForSettings(previousStore.All(), settings))
        {
            services.Store.Set(snapshot);
        }

        UpdateTrayFromSnapshots();
        UpdateDockedOverview();
        UpdatePopover();
    }

    private void UpdateTrayFromSnapshots()
    {
        if (services is null || tray is null)
        {
            return;
        }

        tray.Update(BuildTrayDisplay(services.Store.All()));
    }

    private void UpdateDockedOverview()
    {
        if (services is null)
        {
            return;
        }

        if (!services.Settings.DockOverviewNearTaskbar)
        {
            dockedOverview?.Close();
            dockedOverview = null;
            return;
        }

        var viewModel = new DockedOverviewViewModel(
            services.Store.All(),
            services.Settings.ShowUsageAsUsed,
            DateTimeOffset.Now);
        if (dockedOverview is null)
        {
            dockedOverview = new DockedOverviewWindow(viewModel);
            dockedOverview.Left = System.Windows.SystemParameters.WorkArea.Right - dockedOverview.Width - 16;
            dockedOverview.Closed += (_, _) => dockedOverview = null;
            dockedOverview.Show();
            dockedOverview.UpdateLayout();
            dockedOverview.Top = System.Windows.SystemParameters.WorkArea.Bottom - dockedOverview.ActualHeight - 16;
        }
        else
        {
            dockedOverview.DataContext = viewModel;
            dockedOverview.UpdateLayout();
            dockedOverview.Top = System.Windows.SystemParameters.WorkArea.Bottom - dockedOverview.ActualHeight - 16;
        }
    }

    private void UpdatePopover()
    {
        if (services is null || popover?.IsVisible != true)
        {
            return;
        }

        popover.DataContext = new PopoverViewModel(
            services.Store.All(),
            UsageProvider.Codex,
            services.Settings.ShowUsageAsUsed,
            openDashboard: ShowUsageDashboard,
            openSettings: ShowSettings,
            showAbout: ShowAbout,
            quit: Shutdown);
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
            _ => true
        }).ToArray();

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
}
