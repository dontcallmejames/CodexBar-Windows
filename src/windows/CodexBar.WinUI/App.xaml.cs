using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CodexBar.Core.Models;
using CodexBar.Tray;
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
    private TrayIconHost? tray;
    private ThemeListener? themeListener;
    private PopoverWindow? popover;
    private Microsoft.UI.Dispatching.DispatcherQueue? uiDispatcher;

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
            shell = await AppHostBuilder.BuildAsync();
            themeListener = new ThemeListener(ProbeSystemTheme);

            // One-shot refresh on startup, fire-and-forget.
            _ = Task.Run(async () =>
            {
                try { await shell.Scheduler.RefreshAllAsync(default); } catch { }
            });

            tray = new TrayIconHost(
                onLeftClick: () => uiDispatcher.TryEnqueue(TogglePopover),
                onSettingsClick: () => tray?.ShowNotification(
                    "Settings",
                    "Settings window is not yet implemented in the WinUI 3 spike. Use the WPF build for now."),
                onAboutClick: () => tray?.ShowNotification(
                    "About CodexBar",
                    "CodexBar WinUI 3 preview (Phase 2 spike). Powered by .NET 9 + Windows App SDK 1.6."),
                onQuitClick: () => uiDispatcher.TryEnqueue(() => Application.Current.Exit()));

            // Show a minimal initial icon — Update() makes it visible.
            tray.Update(new TrayDisplayModel("CodexBar (WinUI 3 preview)", 0.0, IsStale: false));

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
                380, 480,
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

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);
    }
}
