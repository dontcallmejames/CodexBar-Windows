using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Codex;

public sealed record CodexOAuthUsageResponse(
    [property: JsonPropertyName("primary_window")] CodexOAuthWindow? PrimaryWindow,
    [property: JsonPropertyName("secondary_window")] CodexOAuthWindow? SecondaryWindow,
    [property: JsonPropertyName("rate_limit")] CodexOAuthRateLimit? RateLimit,
    [property: JsonPropertyName("credits")] CodexOAuthCredits? Credits,
    [property: JsonPropertyName("account")] CodexOAuthAccount? Account,
    [property: JsonPropertyName("plan_type")] string? PlanType);

public sealed record CodexOAuthRateLimit(
    [property: JsonPropertyName("primary_window")] CodexOAuthWindow? PrimaryWindow,
    [property: JsonPropertyName("secondary_window")] CodexOAuthWindow? SecondaryWindow);

public sealed record CodexOAuthWindow(
    [property: JsonPropertyName("used_percent")] double UsedPercent,
    [property: JsonPropertyName("resets_at")] long? ResetsAt,
    [property: JsonPropertyName("reset_at")] long? ResetAt,
    [property: JsonPropertyName("limit_window_seconds")] int? LimitWindowSeconds);

public sealed record CodexOAuthCredits(
    [property: JsonPropertyName("balance")] JsonElement? Balance,
    [property: JsonPropertyName("has_credits")] bool HasCredits);

public sealed record CodexOAuthAccount(
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("plan_type")] string? PlanType);
