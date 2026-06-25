using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers;
using CodexBar.Core.Providers.Antigravity;
using CodexBar.Core.Providers.Claude;
using CodexBar.Core.Providers.Codex;
using CodexBar.Core.Providers.Copilot;
using CodexBar.Core.Providers.Cursor;
using CodexBar.Core.Providers.Gemini;
using CodexBar.Core.Refresh;
using CodexBar.Core.Settings;
using CodexBar.Core.Startup;
using CodexBar.Core.Updates;
using CodexBar.WinUI.Services;
using Microsoft.UI.Dispatching;

namespace CodexBar.WinUI;

public sealed class AppShell : IDisposable
{
    public IAppPaths Paths { get; }
    public AppSettings Settings { get; private set; }
    public HttpClient HttpClient { get; }
    public HttpClient AntigravityHttpClient { get; }
    public SnapshotStore Store { get; }
    public ProviderRefreshStateRegistry RefreshStates { get; }
    public RefreshScheduler Scheduler { get; }
    public IReadOnlyList<IUsageProvider> Providers { get; }
    public RefreshOrchestrator RefreshOrchestrator { get; }
    public UpdateNotifier UpdateNotifier { get; }
    public IStartupRegistration StartupRegistration { get; }

    public event Action? OnSnapshotsChanged;

    /// <summary>
    /// Persists <paramref name="newSettings"/>, reconfigures providers, restarts the orchestrator
    /// and update notifier, then fires an immediate refresh. This is the single entrypoint for
    /// applying settings changes — mirrors WPF's AppShellController.ApplySettings.
    /// </summary>
    public async Task ApplySettingsAsync(AppSettings newSettings)
    {
        // 1. Stop orchestrator + notifier so the timer doesn't fire mid-reconfigure.
        RefreshOrchestrator.Stop();
        UpdateNotifier.Stop();

        // 2. Rebuild the provider/scheduler list in-place (preserves HttpClient, Store, RefreshStates).
        Scheduler.ReplaceProviders(BuildProviders(newSettings));

        // 3. Update our own Settings cache so the orchestrator's interval provider reads the new value.
        Settings = newSettings;

        // 4. Drop snapshots for any provider that's no longer enabled.
        foreach (var provider in Enum.GetValues<UsageProvider>())
        {
            if (!IsEnabled(newSettings, provider))
                Store.Remove(provider);
        }

        // 5. Persist to disk.
        var settingsStore = new JsonSettingsStore(Paths.SettingsFile);
        await settingsStore.SaveAsync(newSettings, default);

        // 5a. Mirror the LaunchAtStartup setting into the Run registry key. Done after
        // persistence so the on-disk setting and the registry state agree even if the
        // registry write fails (the next launch will reconcile).
        TryApplyStartupRegistration(newSettings.LaunchAtStartup);

        // 6. Restart orchestrator + fire immediate refresh (OnSnapshotsChanged fires via Refreshed).
        RefreshOrchestrator.Start();
        await RefreshOrchestrator.RefreshNowAsync(default);

        // 7. Restart update notifier with optional immediate check.
        if (newSettings.CheckForUpdatesAutomatically)
        {
            UpdateNotifier.Start(TimeSpan.FromHours(24));
            _ = UpdateNotifier.CheckNowAsync(default);
        }
    }

    private static bool IsEnabled(AppSettings s, UsageProvider p) => p switch
    {
        UsageProvider.Codex => s.CodexEnabled,
        UsageProvider.Claude => s.ClaudeEnabled,
        UsageProvider.Cursor => s.CursorEnabled,
        UsageProvider.Gemini => s.GeminiEnabled,
        UsageProvider.Copilot => s.CopilotEnabled,
        UsageProvider.Antigravity => s.AntigravityEnabled,
        _ => true,
    };

    public AppShell(AppSettings settings, DispatcherQueue dispatcherQueue, CancellationToken shutdownToken)
    {
        Paths = new WindowsAppPaths();
        Settings = settings;
        HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // Dedicated client that trusts the Antigravity language server's self-signed loopback cert.
        // Kept separate from HttpClient so this bypass never applies to any other provider.
        AntigravityHttpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request.RequestUri?.Host is "127.0.0.1" or "::1" or "localhost"
        })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        Store = new SnapshotStore();
        RefreshStates = new ProviderRefreshStateRegistry();
        Providers = BuildProviders(settings);
        Scheduler = new RefreshScheduler(Providers, Store, RefreshStates);

        RefreshOrchestrator = new RefreshOrchestrator(
            Scheduler,
            // Match the Settings UI's clamp range (1..1440 minutes). Previously capped
            // at 60, which silently truncated any user-configured value > 1h.
            () => TimeSpan.FromMinutes(Math.Clamp(Settings.RefreshMinutes, 1, 1440)),
            dispatcherQueue,
            shutdownToken);
        RefreshOrchestrator.Refreshed += (_, _) => OnSnapshotsChanged?.Invoke();

        UpdateNotifier = new UpdateNotifier(
            new GitHubUpdateChecker(HttpClient, AppVersionInfo.Current),
            UpdateNotificationPoster.Show,
            dispatcherQueue,
            shutdownToken);

        StartupRegistration = new StartupRegistration();
        // Reconcile the registry with the persisted setting at startup so a manual
        // edit (or an external uninstaller pulling the value) gets corrected.
        TryApplyStartupRegistration(settings.LaunchAtStartup);
    }

    private void TryApplyStartupRegistration(bool enabled)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath)) return;
            StartupRegistration.SetEnabled(enabled, exePath);
        }
        catch
        {
            // Best-effort — never crash the host because a registry write got denied.
        }
    }

    public void Dispose()
    {
        RefreshOrchestrator.Dispose();
        UpdateNotifier.Dispose();
        HttpClient.Dispose();
        AntigravityHttpClient.Dispose();
    }

    private IReadOnlyList<IUsageProvider> BuildProviders(AppSettings settings)
    {
        var list = new List<IUsageProvider>();
        if (settings.CodexEnabled) list.Add(new CodexProvider(HttpClient, Paths));
        if (settings.ClaudeEnabled) list.Add(new ClaudeProvider(HttpClient, Paths, manualCookieHeader: settings.ClaudeManualCookieHeader));
        if (settings.CursorEnabled) list.Add(new CursorProvider(HttpClient, settings.CursorManualCookieHeader));
        if (settings.GeminiEnabled) list.Add(new GeminiProvider(HttpClient, Paths));
        if (settings.CopilotEnabled) list.Add(new CopilotProvider(HttpClient));
        if (settings.AntigravityEnabled) list.Add(new AntigravityProvider(AntigravityHttpClient, new WindowsAntigravityProcessLocator()));
        return list;
    }
}

public static class AppHostBuilder
{
    public static async System.Threading.Tasks.Task<AppShell> BuildAsync(DispatcherQueue dispatcherQueue, CancellationToken shutdownToken)
    {
        var paths = new WindowsAppPaths();
        var settingsStore = new JsonSettingsStore(paths.SettingsFile);
        AppSettings settings;
        try
        {
            settings = await settingsStore.LoadAsync(default);
        }
        catch
        {
            settings = AppSettings.Default;
        }
        return new AppShell(settings, dispatcherQueue, shutdownToken);
    }
}
