using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Core.Settings;
using CodexBar.WinUI.Services;
using CodexBar.WinUI.ViewModels;
using CodexBar.WinUI.Views;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

// TODO: single-instance via AppInstance.FindOrRegisterForKey + Program.Main bootstrap.
//       Deferred to Phase 3.

namespace CodexBar.WinUI;

public partial class App : Application
{
    private AppShell? shell;
    private TrayHost? tray;
    private ThemeListener? themeListener;
    private PopoverWindow? popover;
    private TaskbarDockWindow? dock;
    private SettingsWindow? settingsWindow;
    private AboutWindow? aboutWindow;
    private FirstRunWindow? firstRunWindow;
    private Window? lifetimeAnchor;  // Activated then hidden — keeps app alive without visible UI.
    private Microsoft.UI.Dispatching.DispatcherQueue? uiDispatcher;
    private System.Threading.CancellationTokenSource? shutdownCts;
    private UISettings? uiSettings;

    public App()
    {
        InitializeComponent();

        // Catch unhandled exceptions from XAML/UI thread. Marking Handled=true keeps the app
        // alive — without this any background-thread exception kills the process.
        this.UnhandledException += (_, e) =>
        {
            try { WriteCrashLog("UnhandledException", e.Exception); } catch { /* ignore */ }
            System.Diagnostics.Debug.WriteLine($"CodexBar UnhandledException: {e.Exception}");
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                try { WriteCrashLog("AppDomain.UnhandledException", ex); } catch { /* ignore */ }
                System.Diagnostics.Debug.WriteLine($"CodexBar AppDomain.UnhandledException: {ex}");
            }
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Capture the UI dispatcher while we are on the UI thread.
        uiDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        try
        {
            shutdownCts = new System.Threading.CancellationTokenSource();
            var paths = new CodexBar.Core.Paths.WindowsAppPaths();
            var settingsFileExisted = System.IO.File.Exists(paths.SettingsFile);

            // WinUI 3 exits the process when the last VISIBLE window closes. Hidden windows
            // don't count. Create a real Window, Activate() once (registers it as alive),
            // then immediately Hide() it so the user never sees it. Only QuitApp closes it.
            lifetimeAnchor = new Window();
            if (lifetimeAnchor.AppWindow is { } anchorAppWindow)
            {
                anchorAppWindow.IsShownInSwitchers = false;
                if (anchorAppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter ap)
                {
                    ap.IsMaximizable = false;
                    ap.IsMinimizable = false;
                    ap.IsResizable = false;
                    ap.SetBorderAndTitleBar(false, false);
                }
                anchorAppWindow.Resize(new Windows.Graphics.SizeInt32(1, 1));
                anchorAppWindow.Move(new Windows.Graphics.PointInt32(-32000, -32000));
            }
            lifetimeAnchor.Activate();
            lifetimeAnchor.AppWindow?.Hide();

            shell = await AppHostBuilder.BuildAsync(uiDispatcher, shutdownCts.Token);
            themeListener = new ThemeListener(ProbeSystemTheme);

            tray = new TrayHost();
            tray.LeftClick += (_, _) => uiDispatcher.TryEnqueue(TogglePopover);
            tray.OnSettingsClick = () => uiDispatcher.TryEnqueue(ShowSettings);
            tray.OnAboutClick = () => uiDispatcher.TryEnqueue(ShowAbout);
            tray.OnQuitClick = () => uiDispatcher.TryEnqueue(QuitApp);
            tray.Show();
            // NotificationInvoked is subscribed in Program.Main before Register() — required by the SDK.

            // One-time tray icon render from any snapshots that may already exist.
            tray.Update(TraySelector.Build(shell.Store.All()));

            // Wire live updates: every completed refresh updates the tray icon and dock.
            shell.OnSnapshotsChanged += () => uiDispatcher.TryEnqueue(() =>
            {
                tray?.Update(TraySelector.Build(shell.Store.All()));
                UpdateTaskbarDock();
            });

            // Fire one immediate refresh + start the periodic timer.
            _ = shell.RefreshOrchestrator.RefreshNowAsync(shutdownCts.Token);
            shell.RefreshOrchestrator.Start();
            UpdateTaskbarDock();

            if (shell.Settings.CheckForUpdatesAutomatically)
            {
                shell.UpdateNotifier.Start(TimeSpan.FromHours(24));
                _ = shell.UpdateNotifier.CheckNowAsync(shutdownCts.Token);
            }

            if (!settingsFileExisted)
            {
                ShowFirstRun();
            }

            // Listen to live system theme changes (fires from background thread).
            // Promoted to a field so the event subscription stays alive for the app's lifetime.
            uiSettings = new UISettings();
            uiSettings.ColorValuesChanged += (_, _) =>
                uiDispatcher.TryEnqueue(() => themeListener?.Refresh());
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnLaunched", ex);
        }
    }

    private void UpdateTaskbarDock()
    {
        try
        {
            if (shell is null) return;

            if (!shell.Settings.DockOverviewNearTaskbar)
            {
                dock?.Close();
                dock = null;
                return;
            }

            var vm = new TaskbarDockViewModel(shell.Store.All(), shell.Settings.ShowUsageAsUsed);
            if (!vm.HasTiles)
            {
                dock?.Close();
                dock = null;
                return;
            }

            if (dock is null)
            {
                dock = new TaskbarDockWindow(vm);
                dock.Closed += (_, _) => dock = null;
                PositionDock(dock);
                dock.Activate();
            }
            else
            {
                dock.SetViewModel(vm);
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("UpdateTaskbarDock", ex);
        }
    }

    private void PositionDock(TaskbarDockWindow window)
    {
        NativeMethods.GetCursorPos(out var pt);
        var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
            new Windows.Graphics.PointInt32(pt.X, pt.Y),
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        var (left, top) = PopoverPositioner.CalculateTaskbarDock(
            400, 84,
            displayArea.WorkArea.X, displayArea.WorkArea.Y,
            displayArea.WorkArea.Width, displayArea.WorkArea.Height);
        window.AppWindow.Move(new Windows.Graphics.PointInt32(left, top));
    }

    private void TogglePopover()
    {
        try
        {
            // Hide-and-show pattern: the popover Window lives for the lifetime of the app.
            // This keeps WinUI 3 from exiting the process on "last window closed", and lets
            // the tray Settings/About flyout items dispatch properly (the popover instance
            // stays valid so PositionNearAnchor can still anchor to its bounds).
            if (popover is not null && popover.AppWindow is not null && popover.AppWindow.IsVisible)
            {
                popover.AppWindow.Hide();
                return;
            }
            if (shell is null || themeListener is null || uiDispatcher is null) return;
            var dispatcher = uiDispatcher;

            if (popover is null)
            {
                var vm = new PopoverViewModel(
                    shell.Store.All(),
                    UsageProvider.Codex,
                    shell.Settings.ShowUsageAsUsed,
                    refreshStates: shell.RefreshStates,
                    openSettings: () => dispatcher.TryEnqueue(ShowSettings),
                    openAbout: () => dispatcher.TryEnqueue(ShowAbout),
                    quit: () => dispatcher.TryEnqueue(QuitApp),
                    openDashboard: () => OpenUriForActiveProvider(ProviderLinks.DashboardUri),
                    openStatusPage: () => OpenUriForActiveProvider(ProviderLinks.StatusUri),
                    openAddAccount: () => dispatcher.TryEnqueue(ShowSettings));

                popover = new PopoverWindow(vm, themeListener);
                popover.Closed += (_, _) => popover = null;
            }
            else
            {
                // Refresh data on each re-open so the popover doesn't show stale snapshots.
                popover.RefreshFromStore(shell.Store.All(), shell.Settings.ShowUsageAsUsed);
            }

            NativeMethods.GetCursorPos(out var pt);
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
                new Windows.Graphics.PointInt32(pt.X, pt.Y),
                Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            var (left, top) = PopoverPositioner.CalculateForCursor(
                pt.X, pt.Y,
                440, 520,
                displayArea.WorkArea.X, displayArea.WorkArea.Y,
                displayArea.WorkArea.Width, displayArea.WorkArea.Height);

            if (popover is null || popover.AppWindow is null) return;
            popover.AppWindow.Move(new Windows.Graphics.PointInt32(left, top));
            popover.AppWindow.Show();
            popover.Activate();
        }
        catch (Exception ex)
        {
            WriteCrashLog("TogglePopover", ex);
        }
    }

    private static CodexBarTheme ProbeSystemTheme()
    {
        var settings = new UISettings();
        var background = settings.GetColorValue(UIColorType.Background);
        // Dark mode -> background is near-black; Light mode -> near-white.
        var luminance = (background.R + background.G + background.B) / 3.0;
        return luminance < 128 ? CodexBarTheme.Dark : CodexBarTheme.Light;
    }

    // Static because subscription happens in Program.Main before App is constructed
    // (Windows AppNotification SDK requires handlers registered before Register()).
    public static void OnNotificationInvoked(
        Microsoft.Windows.AppNotifications.AppNotificationManager sender,
        Microsoft.Windows.AppNotifications.AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments.TryGetValue("action", out var action) && action == "open-release")
        {
            if (args.Arguments.TryGetValue("url", out var url))
            {
                Services.ExternalLauncher.OpenExternalUrl(url);
            }
        }
    }

    private static void WriteCrashLog(string source, Exception error)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "codexbar-winui-crash.log");
            var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(path, $"[{stamp}] {source}: {error}\n\n");
        }
        catch { }
    }

    private void OpenUriForActiveProvider(Func<UsageProvider, Uri> uriFor)
    {
        if (popover?.ViewModel is null) return;
        var provider = popover.ViewModel.ActiveProvider;
        Services.ExternalLauncher.OpenExternalUrl(uriFor(provider));
    }

    private void ShowSettings()
    {
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        if (shell is null) return;

        var vm = new SettingsViewModel(
            shell.Settings,
            () => shell!.Store.All(),
            () => shell!.UpdateNotifier.LatestResult);
        settingsWindow = new SettingsWindow(vm, async newSettings =>
        {
            try { await shell!.ApplySettingsAsync(newSettings); }
            catch (Exception ex) { WriteCrashLog("ApplySettingsAsync", ex); }
        });
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        PositionNearAnchor(settingsWindow);
        settingsWindow.Activate();
    }

    private void ShowAbout()
    {
        if (aboutWindow is not null) { aboutWindow.Activate(); return; }
        aboutWindow = new AboutWindow(new AboutViewModel(CodexBar.Core.Updates.AppVersionInfo.Current));
        aboutWindow.Closed += (_, _) => aboutWindow = null;
        PositionNearAnchor(aboutWindow);
        aboutWindow.Activate();
    }

    private void QuitApp()
    {
        try { popover?.Close(); popover = null; } catch { /* ignore */ }
        try { settingsWindow?.Close(); settingsWindow = null; } catch { /* ignore */ }
        try { aboutWindow?.Close(); aboutWindow = null; } catch { /* ignore */ }
        try { firstRunWindow?.Close(); firstRunWindow = null; } catch { /* ignore */ }
        try { dock?.Close(); dock = null; } catch { /* ignore */ }
        try { lifetimeAnchor?.Close(); lifetimeAnchor = null; } catch { /* ignore */ }

        // Dispose long-lived services so their timers/HTTP clients shut down cleanly.
        try { shutdownCts?.Cancel(); } catch { /* ignore */ }
        try { tray?.Dispose(); tray = null; } catch { /* ignore */ }
        try { shell?.Dispose(); shell = null; } catch { /* ignore */ }
        try { shutdownCts?.Dispose(); shutdownCts = null; } catch { /* ignore */ }

        Application.Current.Exit();
    }

    /// <summary>
    /// Position a secondary window next to the popover's last known position (popover is
    /// persistent for the lifetime of the app, so it always has a valid Position once it's
    /// been opened at least once). If the popover hasn't been opened yet, fall back to
    /// centering on the cursor's display so the window doesn't land under the tray flyout.
    /// </summary>
    private void PositionNearAnchor(Microsoft.UI.Xaml.Window window)
    {
        if (window.AppWindow is null) return;

        if (popover is not null && popover.AppWindow is not null)
        {
            var pos = popover.AppWindow.Position;
            var size = popover.AppWindow.Size;
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
                new Windows.Graphics.PointInt32(pos.X, pos.Y),
                Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            var winSize = window.AppWindow.Size;
            var (left, top) = PopoverPositioner.CalculateNearAnchor(
                winSize.Width, winSize.Height,
                pos.X, pos.Y, size.Width, size.Height,
                displayArea.WorkArea.X, displayArea.WorkArea.Y,
                displayArea.WorkArea.Width, displayArea.WorkArea.Height);
            window.AppWindow.Move(new Windows.Graphics.PointInt32(left, top));
            return;
        }

        // No popover yet — center on the display the cursor is on.
        NativeMethods.GetCursorPos(out var pt);
        var cursorDisplay = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
            new Windows.Graphics.PointInt32(pt.X, pt.Y),
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        var sz = window.AppWindow.Size;
        var centerLeft = cursorDisplay.WorkArea.X + (cursorDisplay.WorkArea.Width - sz.Width) / 2;
        var centerTop = cursorDisplay.WorkArea.Y + (cursorDisplay.WorkArea.Height - sz.Height) / 2;
        window.AppWindow.Move(new Windows.Graphics.PointInt32(centerLeft, centerTop));
    }

    private void ShowFirstRun()
    {
        if (firstRunWindow is not null) { firstRunWindow.Activate(); return; }
        if (shell is null) return;

        var vm = new FirstRunViewModel(shell.Settings);
        firstRunWindow = new FirstRunWindow(
            vm,
            onGetStarted: async settings =>
            {
                try { await shell!.ApplySettingsAsync(settings); }
                catch (Exception ex) { WriteCrashLog("FirstRun GetStarted", ex); }
            },
            onSkip: async () =>
            {
                try
                {
                    var store = new CodexBar.Core.Settings.JsonSettingsStore(shell!.Paths.SettingsFile);
                    await store.SaveAsync(shell.Settings, default);
                }
                catch (Exception ex) { WriteCrashLog("FirstRun Skip", ex); }
            });
        firstRunWindow.Closed += (_, _) => firstRunWindow = null;
        firstRunWindow.Activate();
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);
    }
}
