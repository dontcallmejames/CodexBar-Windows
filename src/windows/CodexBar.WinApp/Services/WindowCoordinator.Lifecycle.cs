using CodexBar.Core.Models;
using CodexBar.Core.Settings;
using CodexBar.WinApp.ViewModels;
using CodexBar.WinApp.Views;
using System.Diagnostics;

// WindowCoordinator.Lifecycle.cs — instance fields, constructor, and window lifecycle methods.

namespace CodexBar.WinApp.Services;

public sealed partial class WindowCoordinator : IDisposable
{
    private readonly AppServices services;
    private readonly JsonSettingsStore settingsStore;
    private readonly Action quit;
    private readonly Action refreshNow;
    private readonly Action<AppSettings> applySettings;
    private readonly CancellationToken shutdownToken;

    private PopoverWindow? popover;
    private TaskbarDockWindow? taskbarDock;
    private SettingsWindow? settingsWindow;
    private FirstRunWindow? firstRunWindow;
    private AboutWindow? aboutWindow;
    private Action<System.Windows.Window>? positionPopover;

    public UpdateCheckResult? LatestUpdateCheck { get; set; }

    public WindowCoordinator(
        AppServices services,
        JsonSettingsStore settingsStore,
        Action quit,
        Action refreshNow,
        Action<AppSettings> applySettings,
        CancellationToken shutdownToken)
    {
        this.services = services;
        this.settingsStore = settingsStore;
        this.quit = quit;
        this.refreshNow = refreshNow;
        this.applySettings = applySettings;
        this.shutdownToken = shutdownToken;
    }

    public void ShowPopover()
    {
        if (popover?.IsVisible == true)
        {
            popover.Close();
            popover = null;
            return;
        }

        var cursorPosition = System.Windows.Forms.Cursor.Position;
        ShowPopoverWindow(UsageProvider.Codex, window => ApplyPopoverPositionNearCursor(window, cursorPosition));
    }

    private void ShowPopoverWindow(UsageProvider activeProvider, Action<System.Windows.Window> positionPopover)
    {
        var viewModel = new PopoverViewModel(
            services.Store.All(),
            activeProvider,
            services.Settings.ShowUsageAsUsed,
            openDashboard: ShowActiveProviderDashboard,
            openSettings: ShowSettings,
            showAbout: ShowAbout,
            quit: quit,
            addAccount: ShowSettings,
            openStatusPage: ShowActiveProviderStatusPage,
            refreshStates: services.RefreshStates);
        popover = new PopoverWindow(viewModel);
        this.positionPopover = positionPopover;
        WirePopoverWindowEvents(popover);
        popover.Show();
        popover.UpdateLayout();
        positionPopover(popover);
        popover.Activate();
    }

    private void WirePopoverWindowEvents(PopoverWindow window)
    {
        window.SizeChanged += Popover_SizeChanged;
        window.Closed += Popover_Closed;
    }

    private void UnwirePopoverWindowEvents(PopoverWindow window)
    {
        window.SizeChanged -= Popover_SizeChanged;
        window.Closed -= Popover_Closed;
    }

    private void Popover_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (sender is System.Windows.Window window && window.IsVisible)
        {
            positionPopover?.Invoke(window);
        }
    }

    private void Popover_Closed(object? sender, EventArgs e)
    {
        if (sender is PopoverWindow window)
        {
            UnwirePopoverWindowEvents(window);
            if (ReferenceEquals(popover, window))
            {
                popover = null;
            }
        }

        positionPopover = null;
    }

    private void ApplyPopoverPositionNearCursor(System.Windows.Window window, System.Drawing.Point cursorPosition)
    {
        window.MaxHeight = WindowCoordinator.CalculatePopoverMaxHeight(System.Windows.SystemParameters.WorkArea, cursorPosition);
        WindowCoordinator.PositionPopoverNearCursor(window, cursorPosition);
    }

    private void PositionPopoverNearDock(System.Windows.Window window)
    {
        if (taskbarDock?.IsVisible != true)
        {
            var cursorPosition = System.Windows.Forms.Cursor.Position;
            ApplyPopoverPositionNearCursor(window, cursorPosition);
            return;
        }

        var dockWidth = taskbarDock.ActualWidth > 0 ? taskbarDock.ActualWidth : taskbarDock.Width;
        var dockTop = taskbarDock.Top;
        window.MaxHeight = WindowCoordinator.CalculatePopoverMaxHeightNearDock(System.Windows.SystemParameters.WorkArea, dockTop);
        var width = WindowCoordinator.WindowWidth(window);
        var height = WindowCoordinator.WindowHeight(window);
        var position = WindowCoordinator.CalculatePopoverPositionNearDock(
            width,
            height,
            System.Windows.SystemParameters.WorkArea,
            taskbarDock.Left,
            dockTop,
            dockWidth);
        window.Left = position.Left;
        window.Top = position.Top;
    }

    private void PositionTaskbarDock()
    {
        if (taskbarDock?.IsVisible != true)
        {
            return;
        }

        var width = taskbarDock.ActualWidth > 0 ? taskbarDock.ActualWidth : taskbarDock.Width;
        var height = taskbarDock.ActualHeight > 0 ? taskbarDock.ActualHeight : taskbarDock.Height;
        var position = WindowCoordinator.CalculateTaskbarDockPosition(width, height, System.Windows.SystemParameters.WorkArea);
        taskbarDock.Left = position.Left;
        taskbarDock.Top = position.Top;
    }

    private void ShowPopoverFromDock()
    {
        if (popover?.IsVisible == true)
        {
            popover.Close();
            popover = null;
            return;
        }

        ShowPopoverWindow(UsageProvider.Codex, PositionPopoverNearDock);
    }

    public async void HideTaskbarDock()
    {
        var settings = services.Settings with { DockOverviewNearTaskbar = false };
        try
        {
            await settingsStore.SaveAsync(settings, shutdownToken);
            applySettings(settings);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(
                error.Message,
                "CodexBar Settings",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    public void ShowSettings()
    {
        try
        {
            if (settingsWindow?.IsVisible == true)
            {
                settingsWindow.Activate();
                return;
            }

            settingsWindow = new SettingsWindow(
                services.Settings,
                settingsStore,
                services.Paths,
                services.Store.All(),
                services.VersionInfo,
                LatestUpdateCheck);
            WireSettingsWindowEvents(settingsWindow);
            PositionWindowNearApp(settingsWindow);
            settingsWindow.Show();
            settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            LogWindowError(nameof(ShowSettings), ex);
            settingsWindow = null;
        }
    }

    public void ShowFirstRunOnboarding()
    {
        try
        {
            if (firstRunWindow?.IsVisible == true)
            {
                firstRunWindow.Activate();
                return;
            }

            firstRunWindow = new FirstRunWindow(
                services.Settings,
                settingsStore,
                services.Paths,
                services.Store.All());
            WireFirstRunWindowEvents(firstRunWindow);
            PositionWindowNearApp(firstRunWindow);
            firstRunWindow.Show();
            firstRunWindow.Activate();
        }
        catch (Exception ex)
        {
            LogWindowError(nameof(ShowFirstRunOnboarding), ex);
            firstRunWindow = null;
        }
    }

    private void WireFirstRunWindowEvents(FirstRunWindow window)
    {
        window.OnboardingSaved += FirstRunWindow_OnboardingSaved;
        window.OnboardingSkipped += FirstRunWindow_OnboardingSkipped;
        window.ProviderHelpRequested += FirstRunWindow_ProviderHelpRequested;
        window.Closed += FirstRunWindow_Closed;
    }

    private void UnwireFirstRunWindowEvents(FirstRunWindow window)
    {
        window.OnboardingSaved -= FirstRunWindow_OnboardingSaved;
        window.OnboardingSkipped -= FirstRunWindow_OnboardingSkipped;
        window.ProviderHelpRequested -= FirstRunWindow_ProviderHelpRequested;
        window.Closed -= FirstRunWindow_Closed;
    }

    private void FirstRunWindow_OnboardingSaved(object? sender, AppSettings settings) =>
        applySettings(settings);

    private void FirstRunWindow_OnboardingSkipped(object? sender, AppSettings settings)
    {
    }

    private void FirstRunWindow_ProviderHelpRequested(object? sender, UsageProvider provider) =>
        ShowProviderHelp(provider);

    private void FirstRunWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is FirstRunWindow window)
        {
            UnwireFirstRunWindowEvents(window);
            if (ReferenceEquals(firstRunWindow, window))
            {
                firstRunWindow = null;
            }
        }
    }

    private void WireSettingsWindowEvents(SettingsWindow window)
    {
        window.SettingsSaved += SettingsWindow_SettingsSaved;
        window.BugReportRequested += SettingsWindow_BugReportRequested;
        window.UpdateCheckRequested += SettingsWindow_UpdateCheckRequested;
        window.TestProviderRequested += SettingsWindow_TestProviderRequested;
        window.ProviderHelpRequested += SettingsWindow_ProviderHelpRequested;
        window.Closed += SettingsWindow_Closed;
    }

    private void UnwireSettingsWindowEvents(SettingsWindow window)
    {
        window.SettingsSaved -= SettingsWindow_SettingsSaved;
        window.BugReportRequested -= SettingsWindow_BugReportRequested;
        window.UpdateCheckRequested -= SettingsWindow_UpdateCheckRequested;
        window.TestProviderRequested -= SettingsWindow_TestProviderRequested;
        window.ProviderHelpRequested -= SettingsWindow_ProviderHelpRequested;
        window.Closed -= SettingsWindow_Closed;
    }

    private void SettingsWindow_SettingsSaved(object? sender, AppSettings settings) =>
        applySettings(settings);

    private void SettingsWindow_BugReportRequested(object? sender, EventArgs e) =>
        ShowBugReport();

    private void SettingsWindow_UpdateCheckRequested(object? sender, EventArgs e) =>
        ShowUpdates();

    private void SettingsWindow_TestProviderRequested(object? sender, UsageProvider provider) =>
        TestProvider(provider);

    private void SettingsWindow_ProviderHelpRequested(object? sender, UsageProvider provider) =>
        ShowProviderHelp(provider);

    private void SettingsWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is SettingsWindow window)
        {
            UnwireSettingsWindowEvents(window);
            if (ReferenceEquals(settingsWindow, window))
            {
                settingsWindow = null;
            }
        }
    }

    public void ShowAbout()
    {
        try
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
        catch (Exception ex)
        {
            LogWindowError(nameof(ShowAbout), ex);
            aboutWindow = null;
        }
    }

    private static void LogWindowError(string source, Exception error)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "codexbar-crash.log");
            var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            System.IO.File.AppendAllText(path, $"[{stamp}] WindowCoordinator.{source}: {error}\n\n");
        }
        catch { }
    }

    public void UpdateTaskbarDock()
    {
        if (!services.Settings.DockOverviewNearTaskbar)
        {
            taskbarDock?.Close();
            taskbarDock = null;
            return;
        }

        var viewModel = new TaskbarDockViewModel(
            services.Store.All(),
            services.Settings.ShowUsageAsUsed);
        if (!viewModel.HasTiles)
        {
            taskbarDock?.Close();
            taskbarDock = null;
            return;
        }

        if (taskbarDock is null)
        {
            taskbarDock = new TaskbarDockWindow(
                viewModel,
                ShowPopoverFromDock,
                refreshNow,
                ShowSettings,
                HideTaskbarDock);
            taskbarDock.Closed += (_, _) => taskbarDock = null;
            taskbarDock.Show();
        }
        else
        {
            taskbarDock.DataContext = viewModel;
        }

        taskbarDock.UpdateLayout();
        PositionTaskbarDock();
    }

    public void UpdatePopover()
    {
        if (popover?.IsVisible != true)
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
            quit: quit,
            addAccount: ShowSettings,
            openStatusPage: ShowActiveProviderStatusPage,
            refreshStates: services.RefreshStates);
    }

    public void UpdateSettingsWindow()
    {
        if (settingsWindow?.IsVisible != true)
        {
            return;
        }

        settingsWindow.DataContext = new SettingsViewModel(
            services.Settings,
            services.Paths,
            services.Store.All(),
            services.VersionInfo,
            LatestUpdateCheck);
    }

    public void OnSnapshotsChanged()
    {
        UpdateTaskbarDock();
        UpdatePopover();
        UpdateSettingsWindow();
    }

    public void OnWorkAreaChanged()
    {
        PositionTaskbarDock();
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

        var position = WindowCoordinator.CalculateSettingsPosition(width, height, anchorLeft, anchorTop, anchorWidth, anchorHeight, workArea);
        window.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
        window.Left = position.Left;
        window.Top = position.Top;
    }

    private UsageProvider ActivePopoverProvider() =>
        popover?.DataContext is PopoverViewModel viewModel
            ? viewModel.ActiveProvider
            : UsageProvider.Codex;

    private void ShowActiveProviderDashboard() =>
        OpenUri(ProviderLinks.DashboardUri(ActivePopoverProvider()));

    private void ShowActiveProviderStatusPage() =>
        OpenUri(ProviderLinks.StatusUri(ActivePopoverProvider()));

    internal static void OpenUri(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
    }

    private static void ShowProviderHelp(UsageProvider provider)
    {
        OpenUri(ProviderLinks.SetupUri(provider));
    }

    private async void ShowUpdates()
    {
        if (LatestUpdateCheck?.UpdateAvailable == true && LatestUpdateCheck.ReleaseUri is not null)
        {
            OpenUri(LatestUpdateCheck.ReleaseUri);
            return;
        }

        try
        {
            LatestUpdateCheck = await services.UpdateChecker.CheckAsync(shutdownToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        UpdateSettingsWindow();
        if (LatestUpdateCheck.UpdateAvailable && LatestUpdateCheck.ReleaseUri is not null)
        {
            OpenUri(LatestUpdateCheck.ReleaseUri);
        }

        System.Windows.MessageBox.Show(
            LatestUpdateCheck.StatusText,
            "CodexBar Updates",
            System.Windows.MessageBoxButton.OK,
            LatestUpdateCheck.UpdateAvailable ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.None);
    }

    private async void TestProvider(UsageProvider provider)
    {
        var snapshot = await services.TestProviderAsync(provider, shutdownToken);
        UpdateTaskbarDock();
        UpdatePopover();
        UpdateSettingsWindow();

        var message = string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
            ? $"{snapshot.DisplayName} credentials are working."
            : snapshot.ErrorMessage;
        System.Windows.MessageBox.Show(
            message,
            $"{snapshot.DisplayName} Credential Test",
            System.Windows.MessageBoxButton.OK,
            snapshot.IsStale ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);
    }

    private void ShowBugReport()
    {
        var summary = BugReportBuilder.BuildDiagnosticSummary(
            services.Settings,
            services.Store.All(),
            services.VersionInfo.CurrentTag,
            updateStatus: LatestUpdateCheck);
        try
        {
            System.Windows.Clipboard.SetText(summary);
            OpenUri(ProviderLinks.BugReportUri());
            System.Windows.MessageBox.Show(
                "Diagnostic summary copied. Paste it into the GitHub issue if useful.",
                "CodexBar Bug Report",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            OpenUri(ProviderLinks.BugReportUri());
            System.Windows.MessageBox.Show(
                "Could not copy the diagnostic summary, but the GitHub issue form will still open.",
                "CodexBar Bug Report",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    public void Dispose()
    {
        firstRunWindow?.Close();
        aboutWindow?.Close();
        settingsWindow?.Close();
        taskbarDock?.Close();
        popover?.Close();
    }
}
