using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Claude;

public sealed record ClaudeUsageResponse(
    [property: JsonPropertyName("five_hour")] ClaudeUsageWindow? FiveHour,
    [property: JsonPropertyName("seven_day")] ClaudeUsageWindow? SevenDay,
    [property: JsonPropertyName("seven_day_oauth_apps")] ClaudeUsageWindow? SevenDayOAuthApps,
    [property: JsonPropertyName("seven_day_sonnet")] ClaudeUsageWindow? SevenDaySonnet,
    [property: JsonPropertyName("seven_day_opus")] ClaudeUsageWindow? SevenDayOpus,
    [property: JsonPropertyName("extra_usage")] ClaudeExtraUsage? ExtraUsage,
    [property: JsonPropertyName("account")] ClaudeAccount? Account)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record ClaudeTokenRefreshResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn);

public sealed record ClaudeUsageWindow(
    [property: JsonPropertyName("utilization")] double? Utilization,
    [property: JsonPropertyName("used_percent")] double? UsedPercent,
    [property: JsonPropertyName("resets_at")] JsonElement? ResetsAt);

public sealed record ClaudeExtraUsage(
    [property: JsonPropertyName("is_enabled")] bool? IsEnabled,
    [property: JsonPropertyName("monthly_limit")] decimal? MonthlyLimit,
    [property: JsonPropertyName("monthly_credit_limit")] decimal? MonthlyCreditLimit,
    [property: JsonPropertyName("used_credits")] decimal? UsedCredits,
    [property: JsonPropertyName("used_usd")] decimal? UsedUsd,
    [property: JsonPropertyName("limit_usd")] decimal? LimitUsd,
    [property: JsonPropertyName("utilization")] double? Utilization,
    [property: JsonPropertyName("currency")] string? Currency);

public sealed record ClaudeAccount(
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("email_address")] string? EmailAddress,
    [property: JsonPropertyName("subscription_type")] string? SubscriptionType,
    [property: JsonPropertyName("subscriptionType")] string? SubscriptionTypeCamel,
    [property: JsonPropertyName("rate_limit_tier")] string? RateLimitTier,
    [property: JsonPropertyName("billing_type")] string? BillingType);

public sealed record ClaudeOrganization(
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string>? Capabilities);

public sealed record ClaudeAccountResponse(
    [property: JsonPropertyName("email_address")] string? EmailAddress,
    [property: JsonPropertyName("memberships")] IReadOnlyList<ClaudeMembership>? Memberships);

public sealed record ClaudeMembership(
    [property: JsonPropertyName("organization")] ClaudeMembershipOrganization? Organization);

public sealed record ClaudeMembershipOrganization(
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("rate_limit_tier")] string? RateLimitTier,
    [property: JsonPropertyName("billing_type")] string? BillingType);
