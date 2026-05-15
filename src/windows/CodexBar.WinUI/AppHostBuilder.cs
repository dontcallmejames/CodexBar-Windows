using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers;
using CodexBar.Core.Providers.Claude;
using CodexBar.Core.Providers.Codex;
using CodexBar.Core.Providers.Cursor;
using CodexBar.Core.Providers.Gemini;
using CodexBar.Core.Refresh;
using CodexBar.Core.Settings;
using CodexBar.Core.Updates;
using CodexBar.WinUI.Services;
using Microsoft.UI.Dispatching;

namespace CodexBar.WinUI;

public sealed class AppShell : IDisposable
{
    public IAppPaths Paths { get; }
    public AppSettings Settings { get; }
    public HttpClient HttpClient { get; }
    public SnapshotStore Store { get; }
    public ProviderRefreshStateRegistry RefreshStates { get; }
    public RefreshScheduler Scheduler { get; }
    public IReadOnlyList<IUsageProvider> Providers { get; }
    public RefreshOrchestrator RefreshOrchestrator { get; }
    public UpdateNotifier UpdateNotifier { get; }

    public event Action? OnSnapshotsChanged;

    /// <summary>
    /// Rebuilds the provider list from updated settings.
    /// Call after persisting new settings so the next refresh uses the new configuration.
    /// </summary>
    public void ReconfigureProviders(AppSettings newSettings)
    {
        Scheduler.ReplaceProviders(BuildProviders(newSettings));
    }

    public AppShell(AppSettings settings, DispatcherQueue dispatcherQueue, CancellationToken shutdownToken)
    {
        Paths = new WindowsAppPaths();
        Settings = settings;
        HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        Store = new SnapshotStore();
        RefreshStates = new ProviderRefreshStateRegistry();
        Providers = BuildProviders(settings);
        Scheduler = new RefreshScheduler(Providers, Store, RefreshStates);

        RefreshOrchestrator = new RefreshOrchestrator(
            Scheduler,
            () => TimeSpan.FromMinutes(Math.Clamp(Settings.RefreshMinutes, 1, 60)),
            dispatcherQueue,
            shutdownToken);
        RefreshOrchestrator.Refreshed += (_, _) => OnSnapshotsChanged?.Invoke();

        UpdateNotifier = new UpdateNotifier(
            new GitHubUpdateChecker(HttpClient, AppVersionInfo.Current),
            result => { /* Task 13 wires AppNotification; for now, no-op */ },
            dispatcherQueue,
            shutdownToken);
    }

    public void Dispose()
    {
        RefreshOrchestrator.Dispose();
        UpdateNotifier.Dispose();
        HttpClient.Dispose();
    }

    private IReadOnlyList<IUsageProvider> BuildProviders(AppSettings settings)
    {
        var list = new List<IUsageProvider>();
        if (settings.CodexEnabled) list.Add(new CodexProvider(HttpClient, Paths));
        if (settings.ClaudeEnabled) list.Add(new ClaudeProvider(HttpClient, Paths, manualCookieHeader: settings.ClaudeManualCookieHeader));
        if (settings.CursorEnabled) list.Add(new CursorProvider(HttpClient, settings.CursorManualCookieHeader));
        if (settings.GeminiEnabled) list.Add(new GeminiProvider(HttpClient, Paths));
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
