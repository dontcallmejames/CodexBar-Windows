using System.Collections.Generic;
using System.Net.Http;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers;
using CodexBar.Core.Providers.Claude;
using CodexBar.Core.Providers.Codex;
using CodexBar.Core.Providers.Cursor;
using CodexBar.Core.Providers.Gemini;
using CodexBar.Core.Refresh;
using CodexBar.Core.Settings;

namespace CodexBar.WinUI;

public sealed class AppShell
{
    public IAppPaths Paths { get; }
    public AppSettings Settings { get; }
    public HttpClient HttpClient { get; }
    public SnapshotStore Store { get; }
    public ProviderRefreshStateRegistry RefreshStates { get; }
    public RefreshScheduler Scheduler { get; }
    public IReadOnlyList<IUsageProvider> Providers { get; }

    public AppShell(AppSettings settings)
    {
        Paths = new WindowsAppPaths();
        Settings = settings;
        HttpClient = new HttpClient { Timeout = System.TimeSpan.FromSeconds(30) };
        Store = new SnapshotStore();
        RefreshStates = new ProviderRefreshStateRegistry();
        Providers = BuildProviders(settings);
        Scheduler = new RefreshScheduler(Providers, Store, RefreshStates);
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
    public static async System.Threading.Tasks.Task<AppShell> BuildAsync()
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
        return new AppShell(settings);
    }
}
