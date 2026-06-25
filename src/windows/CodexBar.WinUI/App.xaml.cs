using System;
using System.Collections.Generic;
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

    // System-wide hotkey: subclasses the lifetime-anchor HWND so it can listen
    // for WM_HOTKEY without needing a separate message-only window.
    private const int HotkeyId = 0xC0DE;
    private IntPtr anchorHwnd = IntPtr.Zero;
    private IntPtr originalWndProc = IntPtr.Zero;
    private NativeMethods.WndProcDelegate? subclassDelegate;
    private bool hotkeyRegistered;

    // Timestamp of the most recent popover auto-hide caused by clicking outside the popover.
    // When the user clicks the tray icon to dismiss an open popover, the deactivation fires
    // BEFORE the tray LeftClick handler, so without this guard TogglePopover would see the
    // popover as hidden and immediately re-open it. If a deactivation just happened within
    // this window, we treat the tray click as the dismiss and swallow it.
    private DateTime popoverDismissedAt = DateTime.MinValue;
    private static readonly TimeSpan PopoverDismissDebounce = TimeSpan.FromMilliseconds(300);

    // Providers we have already posted a "sign in again" toast for. Mutated ONLY inside the
    // uiDispatcher callback in OnLaunched's OnSnapshotsChanged handler, so no locking needed.
    // A provider is removed once its snapshot returns to AuthState.None so a later expiry
    // re-notifies the user exactly once.
    private readonly HashSet<UsageProvider> authNotified = new();

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

            // Install WM_HOTKEY subclass on the anchor so the global hotkey can route here.
            try
            {
                anchorHwnd = WinRT.Interop.WindowNative.GetWindowHandle(lifetimeAnchor);
                InstallHotkeySubclass(anchorHwnd);
            }
            catch (Exception ex) { WriteCrashLog("InstallHotkeySubclass", ex); }

            shell = await AppHostBuilder.BuildAsync(uiDispatcher, shutdownCts.Token);
            themeListener = new ThemeListener(ProbeSystemTheme);

            tray = new TrayHost();
            tray.LeftClick += (_, _) => uiDispatcher.TryEnqueue(() => TogglePopover(PopoverAnchor.Cursor));
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
                var snapshots = shell.Store.All();
                tray?.Update(TraySelector.Build(snapshots));
                UpdateTaskbarDock();
                NotifyAuthExpiries(snapshots);
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

            // Register the configured global hotkey now that settings have loaded.
            TryRegisterGlobalHotkey(shell.Settings);

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

    /// <summary>
    /// Post a one-time "sign in again" toast for each provider whose snapshot flipped to
    /// AuthState.RequiresAuthentication, and clear the per-provider flag once it recovers so a
    /// later expiry re-notifies once. Runs only inside the uiDispatcher callback, so the
    /// authNotified set needs no synchronization.
    /// </summary>
    private void NotifyAuthExpiries(IReadOnlyList<UsageSnapshot> snapshots)
    {
        try
        {
            foreach (var snapshot in snapshots)
            {
                if (snapshot.AuthState == AuthState.RequiresAuthentication)
                {
                    if (authNotified.Add(snapshot.Provider))
                    {
                        AuthNotificationPoster.Show(
                            snapshot.Provider,
                            snapshot.DisplayName,
                            snapshot.ErrorMessage ?? "Your sign-in expired. Reconnect to keep usage updating.");
                    }
                }
                else
                {
                    authNotified.Remove(snapshot.Provider);
                }
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("NotifyAuthExpiries", ex);
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

            var snapshots = shell.Store.All();
            var showUsageAsUsed = shell.Settings.ShowUsageAsUsed;

            if (dock is null)
            {
                var vm = new TaskbarDockViewModel(snapshots, showUsageAsUsed);
                if (!vm.HasTiles) return;
                dock = new TaskbarDockWindow(vm);
                dock.Closed += (_, _) => dock = null;
                PositionDock(dock);
                dock.Activate();
            }
            else
            {
                dock.ReconcileFrom(snapshots, showUsageAsUsed);
                if (!dock.ViewModel.HasTiles)
                {
                    dock.Close();
                    dock = null;
                }
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

    private enum PopoverAnchor
    {
        /// <summary>Place the popover next to the mouse cursor (canonical tray-click behavior).</summary>
        Cursor,
        /// <summary>Place the popover in the bottom-right corner of the primary work area, just above the system tray.</summary>
        SystemTray
    }

    private void TogglePopover(PopoverAnchor anchor = PopoverAnchor.Cursor)
    {
        try
        {
            // If the popover was just dismissed via deactivation (e.g. the same tray click
            // that's firing right now caused the popover to lose focus and auto-hide), treat
            // this click as the close action and don't re-open.
            if (DateTime.UtcNow - popoverDismissedAt < PopoverDismissDebounce)
            {
                popoverDismissedAt = DateTime.MinValue;
                return;
            }

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

            var enabledProviders = ResolveEnabledProviders(shell.Settings);

            if (popover is null)
            {
                var vm = new PopoverViewModel(
                    shell.Store.All(),
                    enabledProviders.Count > 0 ? enabledProviders[0] : UsageProvider.Codex,
                    shell.Settings.ShowUsageAsUsed,
                    enabledProviders: enabledProviders,
                    refreshStates: shell.RefreshStates,
                    openSettings: () => dispatcher.TryEnqueue(ShowSettings),
                    openAbout: () => dispatcher.TryEnqueue(ShowAbout),
                    quit: () => dispatcher.TryEnqueue(QuitApp),
                    openDashboard: () => OpenUriForActiveProvider(ProviderLinks.DashboardUri),
                    openStatusPage: () => OpenUriForActiveProvider(ProviderLinks.StatusUri),
                    openAddAccount: () => dispatcher.TryEnqueue(ShowSettings),
                    openReconnect: p => Services.ExternalLauncher.OpenExternalUrl(ProviderLinks.SetupUri(p)));

                popover = new PopoverWindow(vm, themeListener);
                popover.Closed += (_, _) => popover = null;

                // Auto-hide on deactivation (clicking outside the popover dismisses it,
                // matching the canonical NSPopover / Windows flyout behavior).
                popover.Activated += (sender, e) =>
                {
                    if (e.WindowActivationState != WindowActivationState.Deactivated) return;
                    if (sender is not Window w || w.AppWindow is null || !w.AppWindow.IsVisible) return;
                    popoverDismissedAt = DateTime.UtcNow;
                    w.AppWindow.Hide();
                };
            }
            else
            {
                // Refresh data on each re-open so the popover doesn't show stale snapshots.
                popover.RefreshFromStore(shell.Store.All(), enabledProviders, shell.Settings.ShowUsageAsUsed);
            }

            // Anchor positioning depends on what triggered the toggle. Tray clicks
            // happen with the cursor over the tray icon, so CalculateForCursor lands
            // the popover near the tray naturally. Hotkey triggers can fire with the
            // cursor anywhere on any monitor — fall back to the tray's actual home
            // (bottom-right of the PRIMARY work area) so the popover always opens
            // somewhere predictable and on screen.
            int left, top;
            if (anchor == PopoverAnchor.SystemTray)
            {
                var primary = Microsoft.UI.Windowing.DisplayArea.Primary;
                (left, top) = PopoverPositioner.CalculateTaskbarDock(
                    440, 520,
                    primary.WorkArea.X, primary.WorkArea.Y,
                    primary.WorkArea.Width, primary.WorkArea.Height);
            }
            else
            {
                NativeMethods.GetCursorPos(out var pt);
                var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
                    new Windows.Graphics.PointInt32(pt.X, pt.Y),
                    Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                (left, top) = PopoverPositioner.CalculateForCursor(
                    pt.X, pt.Y,
                    440, 520,
                    displayArea.WorkArea.X, displayArea.WorkArea.Y,
                    displayArea.WorkArea.Width, displayArea.WorkArea.Height);
            }

            if (popover is null || popover.AppWindow is null) return;
            popover.AppWindow.Move(new Windows.Graphics.PointInt32(left, top));
            popover.AppWindow.Show();
            popover.Activate();

            // Force the popover above whatever's currently in front. Windows' foreground-lock
            // routinely denies Activate() when the trigger came from a background process
            // (tray icon), so without this the popover gets buried under the active app.
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(popover);
            NativeMethods.BringToFront(hwnd);
        }
        catch (Exception ex)
        {
            WriteCrashLog("TogglePopover", ex);
        }
    }

    /// <summary>
    /// Returns the providers the user has enabled in Settings, in popover tab order.
    /// The popover renders one tab per provider in this list, regardless of whether the
    /// snapshot store has data for the provider yet.
    /// </summary>
    private static IReadOnlyList<UsageProvider> ResolveEnabledProviders(AppSettings settings)
    {
        var enabled = new List<UsageProvider>(5);
        if (settings.CodexEnabled) enabled.Add(UsageProvider.Codex);
        if (settings.ClaudeEnabled) enabled.Add(UsageProvider.Claude);
        if (settings.CursorEnabled) enabled.Add(UsageProvider.Cursor);
        if (settings.GeminiEnabled) enabled.Add(UsageProvider.Gemini);
        if (settings.CopilotEnabled) enabled.Add(UsageProvider.Copilot);
        return enabled;
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
        if (args.Arguments.TryGetValue("action", out var action) && action is "open-release" or "open-setup")
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
        if (settingsWindow is not null) { settingsWindow.Activate(); BringWindowToFront(settingsWindow); return; }
        if (shell is null) return;

        var installer = new CodexBar.Core.Updates.UpdateInstaller(
            shell.HttpClient,
            CodexBar.Core.Updates.AppVersionInfo.Current);
        var launcher = new Services.UpdateLauncher();

        var vm = new SettingsViewModel(
            shell.Settings,
            () => shell!.Store.All(),
            () => shell!.UpdateNotifier.LatestResult,
            installer,
            installerPath =>
            {
                var ok = launcher.LaunchAndDetach(installerPath, out var err);
                return (ok, err);
            },
            () => uiDispatcher?.TryEnqueue(QuitApp),
            checkForUpdates: () => shell!.UpdateNotifier.CheckNowAsync(shutdownCts?.Token ?? System.Threading.CancellationToken.None));

        // Refresh the VM's CanInstallUpdate / status text whenever the notifier picks up new data.
        EventHandler? handler = null;
        handler = (_, _) => uiDispatcher?.TryEnqueue(() => vm.UpdateAvailableStatus());
        shell.UpdateNotifier.ResultChanged += handler;

        settingsWindow = new SettingsWindow(vm, async newSettings =>
        {
            try
            {
                await shell!.ApplySettingsAsync(newSettings);
                // Re-register the hotkey after settings change. Conflicts are logged
                // to the crash log — Save_Click closes the window so a teaching tip
                // here would never be seen by the user. The rebind dialog handles
                // interactive conflict feedback separately.
                TryRegisterGlobalHotkey(newSettings);
            }
            catch (Exception ex) { WriteCrashLog("ApplySettingsAsync", ex); }
        });
        settingsWindow.Closed += (_, _) =>
        {
            try { if (shell is not null) shell.UpdateNotifier.ResultChanged -= handler; } catch { /* ignore */ }
            settingsWindow = null;
        };
        PositionNearAnchor(settingsWindow);
        settingsWindow.Activate();
        BringWindowToFront(settingsWindow);
    }

    private void ShowAbout()
    {
        if (aboutWindow is not null) { aboutWindow.Activate(); BringWindowToFront(aboutWindow); return; }
        aboutWindow = new AboutWindow(new AboutViewModel(CodexBar.Core.Updates.AppVersionInfo.Current));
        aboutWindow.Closed += (_, _) => aboutWindow = null;
        PositionNearAnchor(aboutWindow);
        aboutWindow.Activate();
        BringWindowToFront(aboutWindow);
    }

    private static void BringWindowToFront(Window window)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            NativeMethods.BringToFront(hwnd);
        }
        catch { /* ignore — best-effort foreground promotion */ }
    }

    private bool TryRegisterGlobalHotkey(AppSettings settings)
    {
        if (anchorHwnd == IntPtr.Zero) return false;

        // Always unregister the previous binding first so re-registration after a
        // settings change doesn't leave the old combo live.
        if (hotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(anchorHwnd, HotkeyId);
            hotkeyRegistered = false;
        }

        if (!settings.EnableGlobalHotkey) return true;
        if (!HotkeyParser.TryParse(settings.GlobalHotkey, out var parsed))
        {
            WriteCrashLog("TryRegisterGlobalHotkey", new InvalidOperationException(
                $"Unparseable hotkey '{settings.GlobalHotkey}'"));
            return false;
        }

        // OR MOD_NOREPEAT into the modifier flags so holding the hotkey only fires
        // WM_HOTKEY once, not on every key-repeat tick. HotkeyParser keeps returning
        // the user's intended modifiers — no-repeat is a registration choice.
        if (NativeMethods.RegisterHotKey(anchorHwnd, HotkeyId, parsed.Modifiers | NativeMethods.MOD_NOREPEAT, parsed.VirtualKey))
        {
            hotkeyRegistered = true;
            return true;
        }

        WriteCrashLog("TryRegisterGlobalHotkey", new InvalidOperationException(
            $"RegisterHotKey failed for '{settings.GlobalHotkey}' — combination likely in use by another app."));
        return false;
    }

    private void InstallHotkeySubclass(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        subclassDelegate = HotkeyWndProc;
        var fnPtr = Marshal.GetFunctionPointerForDelegate(subclassDelegate);
        originalWndProc = NativeMethods.SetWindowLongPtrCompat(hwnd, NativeMethods.GWLP_WNDPROC, fnPtr);
    }

    private IntPtr HotkeyWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            try { uiDispatcher?.TryEnqueue(() => TogglePopover(PopoverAnchor.SystemTray)); }
            catch (Exception ex) { WriteCrashLog("WM_HOTKEY dispatch", ex); }
            return IntPtr.Zero;
        }
        return NativeMethods.CallWindowProc(originalWndProc, hwnd, msg, wParam, lParam);
    }

    private void QuitApp()
    {
        try
        {
            if (hotkeyRegistered && anchorHwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(anchorHwnd, HotkeyId);
                hotkeyRegistered = false;
            }
        }
        catch { /* ignore */ }

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

        // WM_HOTKEY and the WNDPROC slot for our anchor subclass.
        public const uint WM_HOTKEY = 0x0312;
        public const int GWLP_WNDPROC = -4;

        // MOD_NOREPEAT (winuser.h): suppress repeated WM_HOTKEY messages while the
        // hotkey is held down. Without this, holding Ctrl+Alt+U fires the popover
        // toggle dozens of times per second, making it flicker open/closed.
        public const uint MOD_NOREPEAT = 0x4000;

        public delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // 32-/64-bit indirection: user32 exports SetWindowLong on x86 and SetWindowLongPtr on x64.
        public static IntPtr SetWindowLongPtrCompat(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
            IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : SetWindowLong32(hWnd, nIndex, dwNewLong);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static readonly IntPtr HWND_TOPMOST = new(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;

        /// <summary>
        /// Force a window to the front even when Windows' foreground-lock would
        /// otherwise deny SetForegroundWindow from a background process (e.g. a
        /// tray icon click). Flips topmost on then off so the window pops above
        /// whatever's currently in front without becoming permanently topmost.
        /// </summary>
        public static void BringToFront(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            SetForegroundWindow(hWnd);
        }
    }
}
