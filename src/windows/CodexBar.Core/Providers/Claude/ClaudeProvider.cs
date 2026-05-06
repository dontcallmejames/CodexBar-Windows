using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers;

namespace CodexBar.Core.Providers.Claude;

public sealed class ClaudeProvider : IUsageProvider
{
    private static readonly Uri OAuthUsageUri = new("https://api.anthropic.com/api/oauth/usage");
    private static readonly Uri OrganizationsUri = new("https://claude.ai/api/organizations");

    private readonly HttpClient httpClient;
    private readonly IAppPaths paths;
    private readonly string? manualCookieHeader;

    public ClaudeProvider(HttpClient httpClient, IAppPaths paths, string? manualCookieHeader = null)
    {
        this.httpClient = httpClient;
        this.paths = paths;
        this.manualCookieHeader = manualCookieHeader;
    }

    public UsageProvider Provider => UsageProvider.Claude;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(paths.ClaudeCredentialsJson))
        {
            var credentials = await ClaudeOAuthCredentials.ReadAsync(paths.ClaudeCredentialsJson, cancellationToken);
            if (!string.IsNullOrWhiteSpace(credentials.AccessToken))
            {
                return await RefreshOAuthAsync(credentials, cancellationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(manualCookieHeader))
        {
            return await RefreshWebAsync(manualCookieHeader, cancellationToken);
        }

        return UsageSnapshot.MissingCredentials(
            UsageProvider.Claude,
            "Claude",
            "Claude OAuth credentials or manual cookie header were not found.");
    }

    private async Task<UsageSnapshot> RefreshOAuthAsync(
        ClaudeOAuthCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, OAuthUsageUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        AddJsonHeaders(request);
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        request.Headers.TryAddWithoutValidation("User-Agent", "CodexBar-Windows");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var usage = await ReadUsageAsync(response, cancellationToken);
        return ClaudeUsageMapper.Map(usage, DateTimeOffset.Now, "oauth", credentials);
    }

    private async Task<UsageSnapshot> RefreshWebAsync(string cookieHeader, CancellationToken cancellationToken)
    {
        using var organizationsRequest = new HttpRequestMessage(HttpMethod.Get, OrganizationsUri);
        AddJsonHeaders(organizationsRequest);
        AddWebHeaders(organizationsRequest, cookieHeader);
        using var organizationsResponse = await httpClient.SendAsync(
            organizationsRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        organizationsResponse.EnsureSuccessStatusCode();

        await using var organizationsStream = await organizationsResponse.Content.ReadAsStreamAsync(cancellationToken);
        var organizations = await JsonSerializer.DeserializeAsync<IReadOnlyList<ClaudeOrganization>>(
            organizationsStream,
            ClaudeUsageMapper.JsonOptions,
            cancellationToken);
        var organization = SelectOrganization(organizations);
        if (string.IsNullOrWhiteSpace(organization?.Uuid))
        {
            throw new InvalidOperationException("Claude organization was not found.");
        }

        using var usageRequest = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"https://claude.ai/api/organizations/{organization.Uuid}/usage"));
        AddJsonHeaders(usageRequest);
        AddWebHeaders(usageRequest, cookieHeader);
        using var usageResponse = await httpClient.SendAsync(
            usageRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        usageResponse.EnsureSuccessStatusCode();

        var usage = await ReadUsageAsync(usageResponse, cancellationToken);
        return ClaudeUsageMapper.Map(usage, DateTimeOffset.Now, "web");
    }

    private static async Task<ClaudeUsageResponse> ReadUsageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<ClaudeUsageResponse>(
            stream,
            ClaudeUsageMapper.JsonOptions,
            cancellationToken) ?? new ClaudeUsageResponse(null, null, null, null, null, null);
    }

    private static ClaudeOrganization? SelectOrganization(IReadOnlyList<ClaudeOrganization>? organizations)
    {
        if (organizations is null || organizations.Count == 0)
        {
            return null;
        }

        return organizations[0];
    }

    private static void AddJsonHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static void AddWebHeaders(HttpRequestMessage request, string cookieHeader)
    {
        request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        request.Headers.TryAddWithoutValidation("User-Agent", "CodexBar-Windows");
    }
}
