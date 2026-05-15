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
    private Microsoft.UI.Dispatching.DispatcherQueue? uiDispatcher;
    private System.Threading.CancellationTokenSource? shutdownCts;

    public App()
    {
        InitializeComponent();
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
            shell = await AppHostBuilder.BuildAsync(uiDispatcher, shutdownCts.Token);
            themeListener = new ThemeListener(ProbeSystemTheme);

            tray = new TrayHost();
            tray.LeftClick += (_, _) => uiDispatcher.TryEnqueue(TogglePopover);
            tray.OnSettingsClick = () => uiDispatcher.TryEnqueue(ShowSettings);
            tray.OnAboutClick = () => uiDispatcher.TryEnqueue(ShowAbout);
            tray.OnQuitClick = () => uiDispatcher.TryEnqueue(() => Application.Current.Exit());
            tray.Show();

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
            var ui = new UISettings();
            ui.ColorValuesChanged += (_, _) =>
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
            460, 84,
            displayArea.WorkArea.X, displayArea.WorkArea.Y,
            displayArea.WorkArea.Width, displayArea.WorkArea.Height);
        window.AppWindow.Move(new Windows.Graphics.PointInt32(left, top));
    }

    private void TogglePopover()
    {
        try
        {
            if (popover is not null && popover.AppWindow is not null && popover.AppWindow.IsVisible)
            {
                popover.Close();
                popover = null;
                return;
            }
            if (shell is null || themeListener is null || uiDispatcher is null) return;
            var dispatcher = uiDispatcher;

            var vm = new PopoverViewModel(
                shell.Store.All(),
                UsageProvider.Codex,
                shell.Settings.ShowUsageAsUsed,
                refreshStates: shell.RefreshStates,
                openSettings: () => dispatcher.TryEnqueue(ShowSettings),
                openAbout: () => dispatcher.TryEnqueue(ShowAbout),
                quit: () => dispatcher.TryEnqueue(() => Application.Current.Exit()),
                openDashboard: () => OpenUriForActiveProvider(ProviderLinks.DashboardUri),
                openStatusPage: () => OpenUriForActiveProvider(ProviderLinks.StatusUri),
                openAddAccount: () => dispatcher.TryEnqueue(ShowSettings));

            popover = new PopoverWindow(vm, themeListener);
            popover.Closed += (_, _) => popover = null;

            NativeMethods.GetCursorPos(out var pt);
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
                new Windows.Graphics.PointInt32(pt.X, pt.Y),
                Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            var (left, top) = PopoverPositioner.CalculateForCursor(
                pt.X, pt.Y,
                440, 520,
                displayArea.WorkArea.X, displayArea.WorkArea.Y,
                displayArea.WorkArea.Width, displayArea.WorkArea.Height);

            popover.AppWindow.Move(new Windows.Graphics.PointInt32(left, top));
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
        var uri = uriFor(provider);
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    private void ShowSettings()
    {
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        if (shell is null) return;

        var vm = new SettingsViewModel(shell.Settings);
        settingsWindow = new SettingsWindow(vm, async newSettings =>
        {
            try { await shell!.ApplySettingsAsync(newSettings); }
            catch (Exception ex) { WriteCrashLog("ApplySettingsAsync", ex); }
        });
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Activate();
    }

    private void ShowAbout()
    {
        if (aboutWindow is not null) { aboutWindow.Activate(); return; }
        aboutWindow = new AboutWindow(new AboutViewModel(CodexBar.Core.Updates.AppVersionInfo.Current));
        aboutWindow.Closed += (_, _) => aboutWindow = null;
        aboutWindow.Activate();
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
