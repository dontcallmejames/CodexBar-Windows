using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers;

namespace CodexBar.Core.Providers.Claude;

public sealed class ClaudeProvider : IUsageProvider
{
    private static readonly Uri OAuthUsageUri = new("https://api.anthropic.com/api/oauth/usage");
    private static readonly Uri OAuthTokenRefreshUri = new("https://platform.claude.com/v1/oauth/token");
    private static readonly Uri OrganizationsUri = new("https://claude.ai/api/organizations");
    private static readonly Uri AccountUri = new("https://claude.ai/api/account");
    private const string OAuthClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

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
                try
                {
                    return await RefreshOAuthAsync(credentials, cancellationToken);
                }
                catch (HttpRequestException error) when (
                    error.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                    !string.IsNullOrWhiteSpace(credentials.RefreshToken))
                {
                    var refreshed = await RefreshAccessTokenAsync(credentials, cancellationToken);
                    await ClaudeOAuthCredentials.WriteAsync(paths.ClaudeCredentialsJson, refreshed, cancellationToken);
                    return await RefreshOAuthAsync(refreshed, cancellationToken);
                }
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
        ThrowIfRateLimited(response);
        response.EnsureSuccessStatusCode();

        var usage = await ReadUsageAsync(response, cancellationToken);
        return ClaudeUsageMapper.Map(usage, DateTimeOffset.Now, "oauth", credentials);
    }

    private async Task<ClaudeOAuthCredentials> RefreshAccessTokenAsync(
        ClaudeOAuthCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, OAuthTokenRefreshUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", "CodexBar-Windows");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = credentials.RefreshToken!,
            ["client_id"] = OAuthClientId
        });

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        ThrowIfRateLimited(response);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var tokenResponse = await JsonSerializer.DeserializeAsync<ClaudeTokenRefreshResponse>(
            stream,
            ClaudeUsageMapper.JsonOptions,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            throw new InvalidOperationException("Claude OAuth refresh response did not include an access token.");
        }

        return credentials with
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
                ? credentials.RefreshToken
                : tokenResponse.RefreshToken,
            ExpiresAt = DateTimeOffset.Now.AddSeconds(tokenResponse.ExpiresIn ?? 3600)
        };
    }

    private static void ThrowIfRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
        {
            return;
        }

        var retryText = response.Headers.RetryAfter?.Delta is { } delta
            ? $" Retry in {FormatRetryAfter(delta)}."
            : string.Empty;
        throw new InvalidOperationException($"Claude usage API is rate limited.{retryText}");
    }

    private static string FormatRetryAfter(TimeSpan delta)
    {
        if (delta.TotalMinutes < 1)
        {
            return $"{Math.Max(1, (int)Math.Ceiling(delta.TotalSeconds))}s";
        }

        if (delta.TotalHours < 1)
        {
            return $"{(int)Math.Ceiling(delta.TotalMinutes)}m";
        }

        return $"{(int)Math.Ceiling(delta.TotalHours)}h";
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
            Account = MergeAccount(usage.Account, account),
            ExtraUsage = MergeExtraUsage(usage.ExtraUsage, overage)
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

    private static ClaudeAccount? MergeAccount(ClaudeAccount? usage, ClaudeAccount? enrichment)
    {
        if (usage is null)
        {
            return enrichment;
        }

        if (enrichment is null)
        {
            return usage;
        }

        return usage with
        {
            Email = CoalesceText(usage.Email, enrichment.Email),
            EmailAddress = CoalesceText(usage.EmailAddress, enrichment.EmailAddress),
            SubscriptionType = CoalesceText(usage.SubscriptionType, enrichment.SubscriptionType),
            SubscriptionTypeCamel = CoalesceText(usage.SubscriptionTypeCamel, enrichment.SubscriptionTypeCamel),
            RateLimitTier = CoalesceText(usage.RateLimitTier, enrichment.RateLimitTier),
            BillingType = CoalesceText(usage.BillingType, enrichment.BillingType)
        };
    }

    private static ClaudeExtraUsage? MergeExtraUsage(ClaudeExtraUsage? usage, ClaudeExtraUsage? enrichment)
    {
        if (usage is null)
        {
            return enrichment;
        }

        if (enrichment is null)
        {
            return usage;
        }

        return usage with
        {
            IsEnabled = usage.IsEnabled ?? enrichment.IsEnabled,
            MonthlyLimit = usage.MonthlyLimit ?? enrichment.MonthlyLimit,
            MonthlyCreditLimit = usage.MonthlyCreditLimit ?? enrichment.MonthlyCreditLimit,
            UsedCredits = usage.UsedCredits ?? enrichment.UsedCredits,
            UsedUsd = usage.UsedUsd ?? enrichment.UsedUsd,
            LimitUsd = usage.LimitUsd ?? enrichment.LimitUsd,
            Utilization = usage.Utilization ?? enrichment.Utilization,
            Currency = CoalesceText(usage.Currency, enrichment.Currency)
        };
    }

    private static string? CoalesceText(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

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
