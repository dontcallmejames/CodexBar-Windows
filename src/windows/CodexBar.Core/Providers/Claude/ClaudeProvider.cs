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
    private static readonly Uri AccountUri = new("https://claude.ai/api/account");

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
            if (!string.IsNullOrWhiteSpace(credentials.AccessToken) && HasUserProfileScope(credentials))
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
        var account = await ReadAccountAsync(cookieHeader, organization.Uuid, cancellationToken);
        var overage = await ReadOverageAsync(cookieHeader, organization.Uuid, cancellationToken);
        var merged = usage with
        {
            Account = usage.Account ?? account,
            ExtraUsage = usage.ExtraUsage ?? overage
        };

        return ClaudeUsageMapper.Map(merged, DateTimeOffset.Now, "web");
    }

    private async Task<ClaudeAccount?> ReadAccountAsync(
        string cookieHeader,
        string organizationUuid,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AccountUri);
            AddJsonHeaders(request);
            AddWebHeaders(request, cookieHeader);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var account = await JsonSerializer.DeserializeAsync<ClaudeAccountResponse>(
                stream,
                ClaudeUsageMapper.JsonOptions,
                cancellationToken);
            var membership = SelectMembership(account?.Memberships, organizationUuid);
            return new ClaudeAccount(
                null,
                account?.EmailAddress,
                null,
                null,
                membership?.Organization?.RateLimitTier,
                membership?.Organization?.BillingType);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<ClaudeExtraUsage?> ReadOverageAsync(
        string cookieHeader,
        string organizationUuid,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                new Uri($"https://claude.ai/api/organizations/{organizationUuid}/overage_spend_limit"));
            AddJsonHeaders(request);
            AddWebHeaders(request, cookieHeader);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<ClaudeExtraUsage>(
                stream,
                ClaudeUsageMapper.JsonOptions,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
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

    private static ClaudeMembership? SelectMembership(IReadOnlyList<ClaudeMembership>? memberships, string organizationUuid)
    {
        if (memberships is null || memberships.Count == 0)
        {
            return null;
        }

        return memberships.FirstOrDefault(membership => membership.Organization?.Uuid == organizationUuid)
            ?? memberships[0];
    }

    private static bool HasUserProfileScope(ClaudeOAuthCredentials credentials) =>
        credentials.Scopes.Any(scope => string.Equals(scope, "user:profile", StringComparison.Ordinal));

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
