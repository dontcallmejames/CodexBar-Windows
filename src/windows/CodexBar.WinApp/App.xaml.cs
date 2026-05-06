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
    private TrayIconHost? tray;
    private PopoverWindow? popover;
    private bool isShuttingDown;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new WindowsAppPaths();
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

    private static void ShowSettings()
    {
        const string message = "Open settings from the tray menu after Task 9 adds the settings window.";
        System.Windows.MessageBox.Show(message, "CodexBar");
    }

    private static void ShowAbout()
    {
        const string message = "CodexBar for Windows";
        System.Windows.MessageBox.Show(message, "About CodexBar");
    }

    private void UpdateTrayFromSnapshots()
    {
        if (services is null || tray is null)
        {
            return;
        }

        tray.Update(BuildTrayDisplay(services.Store.All()));
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
