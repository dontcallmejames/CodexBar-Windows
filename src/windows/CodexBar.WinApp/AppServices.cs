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

namespace CodexBar.WinApp;

public sealed class AppServices : IDisposable
{
    public AppServices(IAppPaths paths, AppSettings settings)
    {
        Paths = paths;
        Settings = settings;
        Store = new SnapshotStore();
        HttpClient = new HttpClient();
        VersionInfo = AppVersionInfo.Current;
        UpdateChecker = new GitHubUpdateChecker(HttpClient, VersionInfo);
        Providers = BuildProviders(settings);
        Scheduler = new RefreshScheduler(Providers, Store);
    }

    public IAppPaths Paths { get; }
    public AppSettings Settings { get; }
    public SnapshotStore Store { get; }
    public HttpClient HttpClient { get; }
    public AppVersionInfo VersionInfo { get; }
    public IUpdateChecker UpdateChecker { get; }
    public IReadOnlyList<IUsageProvider> Providers { get; }
    public RefreshScheduler Scheduler { get; }

    public void Dispose()
    {
        HttpClient.Dispose();
    }

    public async Task<UsageSnapshot> TestProviderAsync(UsageProvider provider, CancellationToken cancellationToken)
    {
        var usageProvider = Providers.FirstOrDefault(candidate => candidate.Provider == provider);
        if (usageProvider is null)
        {
            var disabled = UsageSnapshot.MissingCredentials(
                provider,
                ProviderDisplayName(provider),
                $"{ProviderDisplayName(provider)} is disabled in Settings.");
            Store.Set(disabled);
            return disabled;
        }

        try
        {
            var snapshot = await usageProvider.RefreshAsync(cancellationToken);
            Store.Set(snapshot);
            return snapshot;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            var previous = Store.Get(provider);
            var snapshot = previous is null
                ? UsageSnapshot.MissingCredentials(provider, ProviderDisplayName(provider), error.Message)
                : previous with { IsStale = true, ErrorMessage = error.Message };
            Store.Set(snapshot);
            return snapshot;
        }
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

        if (settings.CursorEnabled)
        {
            providers.Add(new CursorProvider(HttpClient, settings.CursorManualCookieHeader));
        }

        if (settings.GeminiEnabled)
        {
            providers.Add(new GeminiProvider(HttpClient, Paths));
        }

        return providers;
    }

    private static string ProviderDisplayName(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Codex => "Codex",
            UsageProvider.Claude => "Claude",
            UsageProvider.Cursor => "Cursor",
            UsageProvider.Gemini => "Gemini",
            _ => provider.ToString()
        };
}
