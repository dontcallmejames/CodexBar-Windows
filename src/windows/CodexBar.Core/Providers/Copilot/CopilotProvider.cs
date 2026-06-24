using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Copilot;

public sealed class CopilotProvider : IUsageProvider
{
    public static readonly Uri UsageUri = new("https://api.github.com/copilot_internal/user");

    // Editor-spoofing headers — GitHub returns 403 without them. Versions are pinned to the
    // last-known-good combo from the openusage plugin; bump if GitHub starts rejecting them.
    public const string EditorVersion = "vscode/1.96.2";
    public const string EditorPluginVersion = "copilot-chat/0.26.7";
    public const string UserAgent = "GitHubCopilotChat/0.26.7";
    public const string GithubApiVersion = "2025-04-01";

    private readonly HttpClient httpClient;
    private readonly Func<CancellationToken, Task<CopilotTokenReader.TokenResult>> tokenReader;

    public CopilotProvider(HttpClient httpClient)
        : this(httpClient, CopilotTokenReader.ReadAsync)
    {
    }

    // Test seam: lets unit tests inject a fake token reader without shelling out.
    public CopilotProvider(HttpClient httpClient, Func<CancellationToken, Task<CopilotTokenReader.TokenResult>> tokenReader)
    {
        this.httpClient = httpClient;
        this.tokenReader = tokenReader;
    }

    public UsageProvider Provider => UsageProvider.Copilot;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        var tokenResult = await tokenReader(cancellationToken);
        if (tokenResult.Status != CopilotTokenReader.TokenStatus.Ok || string.IsNullOrWhiteSpace(tokenResult.Token))
        {
            return UsageSnapshot.MissingCredentials(
                UsageProvider.Copilot,
                "Copilot",
                tokenResult.ErrorMessage ?? "Run `gh auth login` to sign in to GitHub.");
        }

        using var request = BuildRequest(tokenResult.Token);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return UsageSnapshot.RequiresAuthentication(
                UsageProvider.Copilot,
                "Copilot",
                "GitHub rejected your token. Re-run `gh auth login` and ensure your account has Copilot access.");
        }

        // Let transient failures (429, 5xx, network/timeout) propagate to RefreshScheduler's
        // generic catch, which keeps the last-good snapshot as stale and applies backoff.
        // We must NOT swallow them into a MissingCredentials snapshot: that carries
        // AuthState.None, so the scheduler would record it as SUCCESS — wiping last-good data
        // and resetting the backoff that protects a struggling endpoint.
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var usage = await JsonSerializer.DeserializeAsync<CopilotUserResponse>(
            stream,
            CopilotUsageMapper.JsonOptions,
            cancellationToken);

        return CopilotUsageMapper.Map(
            usage ?? new CopilotUserResponse(null, null, null, null, null, null),
            DateTimeOffset.Now);
    }

    public static HttpRequestMessage BuildRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, UsageUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("token", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Editor-Version", EditorVersion);
        request.Headers.TryAddWithoutValidation("Editor-Plugin-Version", EditorPluginVersion);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("X-Github-Api-Version", GithubApiVersion);
        return request;
    }
}
