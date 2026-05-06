using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;

namespace CodexBar.Core.Providers.Codex;

public sealed class CodexProvider : IUsageProvider
{
    private static readonly Uri UsageUri = new("https://chatgpt.com/backend-api/wham/usage");

    private readonly HttpClient httpClient;
    private readonly IAppPaths paths;
    private readonly string? codexHome;

    public CodexProvider(HttpClient httpClient, IAppPaths paths, string? codexHome = null)
    {
        this.httpClient = httpClient;
        this.paths = paths;
        this.codexHome = codexHome;
    }

    public UsageProvider Provider => UsageProvider.Codex;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        var authPath = paths.CodexAuthJson(codexHome);
        if (!File.Exists(authPath))
        {
            return UsageSnapshot.MissingCredentials(
                UsageProvider.Codex,
                "Codex",
                "Codex OAuth credentials were not found.");
        }

        var credentials = await CodexOAuthCredentials.ReadAsync(authPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return UsageSnapshot.MissingCredentials(
                UsageProvider.Codex,
                "Codex",
                "Codex OAuth access token was not found.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(credentials.AccountId))
        {
            request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", credentials.AccountId);
        }

        request.Headers.TryAddWithoutValidation("User-Agent", "CodexBar-Windows");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var usage = await JsonSerializer.DeserializeAsync<CodexOAuthUsageResponse>(
            stream,
            CodexOAuthUsageMapper.JsonOptions,
            cancellationToken);

        return CodexOAuthUsageMapper.Map(
            usage ?? new CodexOAuthUsageResponse(null, null, null, null, null, null),
            DateTimeOffset.Now);
    }
}
