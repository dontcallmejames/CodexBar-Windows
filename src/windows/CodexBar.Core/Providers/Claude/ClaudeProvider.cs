using System.Net.Http.Headers;
using System.Text.Json;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Providers;
using CodexBar.Core.Refresh;

namespace CodexBar.Core.Providers.Claude;

public sealed class ClaudeProvider : IUsageProvider
{
    private static readonly Uri OAuthUsageUri = new("https://api.anthropic.com/api/oauth/usage");
    private static readonly Uri OAuthTokenRefreshUri = new("https://platform.claude.com/v1/oauth/token");
    private static readonly Uri OrganizationsUri = new("https://claude.ai/api/organizations");
    private static readonly Uri AccountUri = new("https://claude.ai/api/account");
    private const string OAuthClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    private const string ClaudeCodeUserAgent = "claude-code/2.1.0";
    private const string ClaudeReAuthMessage =
        "Your Claude sign-in expired. Run `claude` in a terminal, then `/login` to reconnect. Or paste a fresh claude.ai cookie in Settings.";

    private readonly HttpClient httpClient;
    private readonly IAppPaths paths;
    private readonly string? manualCookieHeader;
    private readonly ClaudeCodeLocalUsageScanner localUsageScanner;

    public ClaudeProvider(
        HttpClient httpClient,
        IAppPaths paths,
        string? manualCookieHeader = null,
        ClaudeCodeLocalUsageScanner? localUsageScanner = null)
    {
        this.httpClient = httpClient;
        this.paths = paths;
        this.manualCookieHeader = manualCookieHeader;
        this.localUsageScanner = localUsageScanner ?? new ClaudeCodeLocalUsageScanner();
    }

    public UsageProvider Provider => UsageProvider.Claude;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        var snapshot = await RefreshCloudAsync(cancellationToken);
        return MergeLocalUsage(snapshot, cancellationToken);
    }

    private async Task<UsageSnapshot> RefreshCloudAsync(CancellationToken cancellationToken)
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
                catch (HttpRequestException error) when (IsAuthRejection(error.StatusCode))
                {
                    // No refresh token to recover with → the local credential is dead. Tell the
                    // user how to re-authenticate rather than letting a raw 401 propagate.
                    if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
                    {
                        throw new AuthenticationRequiredException(ClaudeReAuthMessage);
                    }

                    try
                    {
                        var refreshed = await RefreshAccessTokenAsync(credentials, cancellationToken);
                        await ClaudeOAuthCredentials.WriteAsync(paths.ClaudeCredentialsJson, refreshed, cancellationToken);
                        return await RefreshOAuthAsync(refreshed, cancellationToken);
                    }
                    catch (HttpRequestException retryError) when (IsAuthRejection(retryError.StatusCode))
                    {
                        // Refresh succeeded transport-wise but the new token is still rejected,
                        // or the refresh endpoint itself rejected the refresh token.
                        throw new AuthenticationRequiredException(ClaudeReAuthMessage);
                    }
                    catch (InvalidOperationException)
                    {
                        // Refresh response was missing an access token — the refresh token is no
                        // longer honored. Surface as re-auth, not an opaque InvalidOperationException.
                        throw new AuthenticationRequiredException(ClaudeReAuthMessage);
                    }
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

    private UsageSnapshot MergeLocalUsage(UsageSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var report = localUsageScanner.Scan(DateTimeOffset.Now, cancellationToken);
            if (report.TodayTotalTokens == 0 && report.Last7DaysTotalTokens == 0)
            {
                return snapshot;
            }

            // We don't track 30 days locally; expose the 7-day rollup in Last30DaysTokens for now.
            return snapshot with
            {
                TodayTokens = report.TodayTotalTokens,
                Last30DaysTokens = report.Last7DaysTotalTokens
            };
        }
        catch (Exception)
        {
            // Local scan must never fail the cloud snapshot — return as-is on any unexpected error.
            return snapshot;
        }
    }

    private async Task<UsageSnapshot> RefreshOAuthAsync(
        ClaudeOAuthCredentials credentials,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, OAuthUsageUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        AddJsonHeaders(request);
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        request.Headers.TryAddWithoutValidation("User-Agent", ClaudeCodeUserAgent);

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

        // A revoked/expired refresh token comes back as 400 invalid_grant (per OAuth) or
        // 401/403. That is an unrecoverable auth failure — surface it as re-auth so the
        // reconnect InfoBar/toast fire, rather than letting EnsureSuccessStatusCode throw a
        // generic HttpRequestException that the scheduler would treat as a transient blip
        // (keeping stale AuthState.None data). Genuine 5xx still propagates as transient.
        if (response.StatusCode is System.Net.HttpStatusCode.BadRequest || IsAuthRejection(response.StatusCode))
        {
            throw new AuthenticationRequiredException(ClaudeReAuthMessage);
        }

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

    private static bool IsAuthRejection(System.Net.HttpStatusCode? statusCode) =>
        statusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden;

    private static void ThrowIfRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode is not (System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable))
        {
            return;
        }

        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date is { } date
                ? date - DateTimeOffset.Now
                : null);

        throw new RateLimitException(
            $"Claude API rate-limited with status {(int)response.StatusCode}.",
            retryAfter);
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
        ThrowIfRateLimited(organizationsResponse);
        if (IsAuthRejection(organizationsResponse.StatusCode))
        {
            throw new AuthenticationRequiredException(ClaudeReAuthMessage);
        }
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
            cancellationToken) ?? new ClaudeUsageResponse(null, null, null, null, null, null, null);
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
