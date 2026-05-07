using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using CodexBar.Tray;
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
    private PopoverWindow? popover;
    private DockedOverviewWindow? dockedOverview;
    private SettingsWindow? settingsWindow;
    private AboutWindow? aboutWindow;
    private IStartupRegistration? startupRegistration;
    private bool isShuttingDown;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new WindowsAppPaths();
        settingsStore = new JsonSettingsStore(paths.SettingsFile);
        var settings = await LoadSettingsOrDefaultAsync(paths, shutdown.Token);
        services = new AppServices(paths, settings);
        startupRegistration = new StartupRegistration(Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0]);
        ApplyStartupRegistration(settings);

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
        aboutWindow?.Close();
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
            openDashboard: ShowActiveProviderDashboard,
            openSettings: ShowSettings,
            showAbout: ShowAbout,
            quit: Shutdown,
            addAccount: ShowSettings,
            openStatusPage: ShowActiveProviderStatusPage);
        popover = new PopoverWindow(viewModel);
        popover.Closed += (_, _) => popover = null;
        var cursorPosition = System.Windows.Forms.Cursor.Position;
        popover.MaxHeight = CalculatePopoverMaxHeight(System.Windows.SystemParameters.WorkArea, cursorPosition);
        popover.SizeChanged += (_, _) =>
        {
            if (popover?.IsVisible == true)
            {
                PositionPopoverNearCursor(popover, cursorPosition);
            }
        };
        popover.Show();
        popover.UpdateLayout();
        PositionPopoverNearCursor(popover, cursorPosition);
        popover.Activate();
    }

    private static void PositionPopoverNearCursor(System.Windows.Window window, System.Drawing.Point cursorPosition)
    {
        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        var position = CalculatePopoverPosition(width, height, System.Windows.SystemParameters.WorkArea, cursorPosition);
        window.Left = position.Left;
        window.Top = position.Top;
    }

    public static (double Left, double Top) CalculatePopoverPosition(
        double width,
        double height,
        System.Windows.Rect workArea,
        System.Drawing.Point cursorPosition)
    {
        const double margin = 16;
        var left = cursorPosition.X - width + 24;
        var bottom = cursorPosition.Y - 12;
        var top = bottom - height;
        var maxLeft = workArea.Right - width - margin;
        var maxTop = Math.Max(workArea.Top + margin, workArea.Bottom - height - margin);

        return (
            Math.Clamp(left, workArea.Left + margin, maxLeft),
            Math.Clamp(top, workArea.Top + margin, maxTop));
    }

    public static double CalculatePopoverMaxHeight(
        System.Windows.Rect workArea,
        System.Drawing.Point cursorPosition)
    {
        const double margin = 16;
        const double trayGap = 12;
        const double minimumHeight = 360;
        var anchoredBottom = Math.Clamp(cursorPosition.Y - trayGap, workArea.Top + margin + minimumHeight, workArea.Bottom - margin);
        return Math.Max(minimumHeight, anchoredBottom - workArea.Top - margin);
    }

    public static (double Left, double Top) CalculateTaskbarDockPosition(
        double width,
        double height,
        System.Windows.Rect workArea)
    {
        const double margin = 16;
        const double taskbarGap = 12;
        var minLeft = workArea.Left + margin;
        var maxLeft = workArea.Right - width - margin;
        var minTop = workArea.Top + margin;
        var maxTop = workArea.Bottom - height - taskbarGap;

        return (
            maxLeft < minLeft ? minLeft : Math.Clamp(maxLeft, minLeft, maxLeft),
            maxTop < minTop ? minTop : Math.Clamp(maxTop, minTop, maxTop));
    }

    private static void ShowUsageDashboard()
    {
        OpenUri(ProviderLinks.DashboardUri(UsageProvider.Codex));
    }

    private static void ShowStatusPage()
    {
        OpenUri(ProviderLinks.StatusUri(UsageProvider.Codex));
    }

    private void ShowActiveProviderDashboard() =>
        OpenUri(ProviderLinks.DashboardUri(ActivePopoverProvider()));

    private void ShowActiveProviderStatusPage() =>
        OpenUri(ProviderLinks.StatusUri(ActivePopoverProvider()));

    private UsageProvider ActivePopoverProvider() =>
        popover?.DataContext is PopoverViewModel viewModel
            ? viewModel.ActiveProvider
            : UsageProvider.Codex;

    private static void OpenUri(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
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

        settingsWindow = new SettingsWindow(services.Settings, settingsStore, services.Paths);
        settingsWindow.SettingsSaved += (_, settings) => ApplySettings(settings);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        PositionWindowNearApp(settingsWindow);
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void PositionWindowNearApp(System.Windows.Window window)
    {
        var width = window.Width > 0 ? window.Width : 560;
        var height = window.Height > 0 ? window.Height : 620;
        var workArea = System.Windows.SystemParameters.WorkArea;
        double anchorLeft;
        double anchorTop;
        double anchorWidth;
        double anchorHeight;

        if (popover?.IsVisible == true)
        {
            anchorLeft = popover.Left;
            anchorTop = popover.Top;
            anchorWidth = popover.ActualWidth > 0 ? popover.ActualWidth : popover.Width;
            anchorHeight = popover.ActualHeight > 0 ? popover.ActualHeight : popover.Height;
        }
        else
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            anchorLeft = cursor.X;
            anchorTop = cursor.Y;
            anchorWidth = 1;
            anchorHeight = 1;
        }

        var position = CalculateSettingsPosition(width, height, anchorLeft, anchorTop, anchorWidth, anchorHeight, workArea);
        window.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
        window.Left = position.Left;
        window.Top = position.Top;
    }

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

    private void ShowAbout()
    {
        if (aboutWindow?.IsVisible == true)
        {
            aboutWindow.Activate();
            return;
        }

        aboutWindow = new AboutWindow();
        aboutWindow.Closed += (_, _) => aboutWindow = null;
        PositionWindowNearApp(aboutWindow);
        aboutWindow.Show();
        aboutWindow.Activate();
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
        var activeServices = services;
        var snapshots = EnsureEnabledProviderSnapshots(
            FilterSnapshotsForSettings(previousStore.All(), settings),
            settings);
        foreach (var snapshot in snapshots)
        {
            services.Store.Set(snapshot);
        }

        UpdateTrayFromSnapshots();
        UpdateDockedOverview();
        UpdatePopover();
        await RefreshServicesAsync(activeServices);
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
            ActivePopoverProvider(),
            services.Settings.ShowUsageAsUsed,
            openDashboard: ShowActiveProviderDashboard,
            openSettings: ShowSettings,
            showAbout: ShowAbout,
            quit: Shutdown,
            addAccount: ShowSettings,
            openStatusPage: ShowActiveProviderStatusPage);
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
}
