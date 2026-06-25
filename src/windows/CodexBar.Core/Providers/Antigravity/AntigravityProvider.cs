using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Antigravity;

public sealed class AntigravityProvider : IUsageProvider
{
    private readonly AntigravityLanguageServerClient client;
    private readonly IAntigravityProcessLocator locator;

    public AntigravityProvider(HttpClient httpClient, IAntigravityProcessLocator locator)
    {
        client = new AntigravityLanguageServerClient(httpClient);
        this.locator = locator;
    }

    public UsageProvider Provider => UsageProvider.Antigravity;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        var candidates = locator.FindCandidates();
        if (candidates.Count == 0)
        {
            return Missing("Antigravity isn't running.");
        }

        foreach (var candidate in candidates)
        {
            using var response = await client.FetchAsync(candidate, cancellationToken);
            if (response is not null)
            {
                return AntigravityUsageMapper.Map(response.Method, response.Document.RootElement, DateTimeOffset.Now);
            }
        }

        return Missing("Antigravity isn't available.");
    }

    private static UsageSnapshot Missing(string message) =>
        UsageSnapshot.MissingCredentials(UsageProvider.Antigravity, "Antigravity", message);
}
