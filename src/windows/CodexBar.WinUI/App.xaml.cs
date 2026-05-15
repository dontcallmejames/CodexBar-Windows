using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CodexBar.Core.Models;
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
    private SettingsWindow? settingsWindow;
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
            shell = await AppHostBuilder.BuildAsync(uiDispatcher, shutdownCts.Token);
            themeListener = new ThemeListener(ProbeSystemTheme);

            tray = new TrayHost();
            tray.LeftClick += (_, _) => uiDispatcher.TryEnqueue(TogglePopover);
            tray.OnSettingsClick = () => uiDispatcher.TryEnqueue(ShowSettings);
            tray.OnAboutClick = () => uiDispatcher.TryEnqueue(ShowAboutPlaceholder);
            tray.OnQuitClick = () => uiDispatcher.TryEnqueue(() => Application.Current.Exit());
            tray.Show();

            // One-time tray icon render from any snapshots that may already exist.
            tray.Update(TraySelector.Build(shell.Store.All()));

            // Wire live updates: every completed refresh updates the tray icon.
            shell.OnSnapshotsChanged += () => uiDispatcher.TryEnqueue(() =>
                tray?.Update(TraySelector.Build(shell.Store.All())));

            // Fire one immediate refresh + start the periodic timer.
            _ = shell.RefreshOrchestrator.RefreshNowAsync(shutdownCts.Token);
            shell.RefreshOrchestrator.Start();

            if (shell.Settings.CheckForUpdatesAutomatically)
            {
                shell.UpdateNotifier.Start(TimeSpan.FromHours(24));
                _ = shell.UpdateNotifier.CheckNowAsync(shutdownCts.Token);
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
            if (shell is null || themeListener is null) return;

            var vm = new PopoverViewModel(
                shell.Store.All(),
                UsageProvider.Codex,
                shell.Settings.ShowUsageAsUsed,
                refreshStates: shell.RefreshStates);

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

    private void ShowSettings()
    {
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        if (shell is null) return;

        var vm = new SettingsViewModel(shell.Settings);
        settingsWindow = new SettingsWindow(vm, async newSettings =>
        {
            await PersistAndApplySettingsAsync(newSettings);
        });
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Activate();
    }

    private async Task PersistAndApplySettingsAsync(AppSettings newSettings)
    {
        if (shell is null) return;
        try
        {
            var store = new JsonSettingsStore(shell.Paths.SettingsFile);
            await store.SaveAsync(newSettings, default);
            shell.ReconfigureProviders(newSettings);
            // Remove snapshots for any newly-disabled providers.
            foreach (var p in System.Enum.GetValues<UsageProvider>())
            {
                if (!IsEnabled(newSettings, p)) shell.Store.Remove(p);
            }
            await shell.RefreshOrchestrator.RefreshNowAsync(default);
        }
        catch (Exception ex)
        {
            WriteCrashLog("PersistAndApplySettingsAsync", ex);
        }
    }

    private static bool IsEnabled(AppSettings s, UsageProvider p) => p switch
    {
        UsageProvider.Codex => s.CodexEnabled,
        UsageProvider.Claude => s.ClaudeEnabled,
        UsageProvider.Cursor => s.CursorEnabled,
        UsageProvider.Gemini => s.GeminiEnabled,
        _ => true,
    };

    private void ShowAboutPlaceholder()
    {
        // TODO Phase 3 Task 9: replace with real About window.
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
