using System.Net.Http;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers;
using CodexBar.Core.Providers.Claude;
using CodexBar.Core.Providers.Codex;
using CodexBar.Core.Refresh;
using CodexBar.Core.Settings;

namespace CodexBar.WinApp;

public sealed class AppServices : IDisposable
{
    public AppServices(IAppPaths paths, AppSettings settings)
    {
        Paths = paths;
        Settings = settings;
        Store = new SnapshotStore();
        HttpClient = new HttpClient();
        Providers = BuildProviders(settings);
        Scheduler = new RefreshScheduler(Providers, Store);
    }

    public IAppPaths Paths { get; }
    public AppSettings Settings { get; }
    public SnapshotStore Store { get; }
    public HttpClient HttpClient { get; }
    public IReadOnlyList<IUsageProvider> Providers { get; }
    public RefreshScheduler Scheduler { get; }

    public void Dispose()
    {
        HttpClient.Dispose();
    }

    private IReadOnlyList<IUsageProvider> BuildProviders(AppSettings settings)
    {
        var providers = new List<IUsageProvider>();
        if (settings.CodexEnabled)
        {
            providers.Add(new CodexProvider(HttpClient, Paths));
        }

        if (settings.ClaudeEnabled)
        {
            providers.Add(new ClaudeProvider(HttpClient, Paths, manualCookieHeader: settings.ClaudeManualCookieHeader));
        }

        return providers;
    }
}
