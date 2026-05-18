using System.Text.Json.Serialization;

namespace CodexBar.Core.Providers.Copilot;

// Models the relevant subset of GET https://api.github.com/copilot_internal/user.
// Paid tiers populate quota_snapshots; free tier populates limited_user_quotas + monthly_quotas.

public sealed record CopilotUserResponse(
    [property: JsonPropertyName("copilot_plan")] string? CopilotPlan,
    [property: JsonPropertyName("quota_snapshots")] CopilotQuotaSnapshots? QuotaSnapshots,
    [property: JsonPropertyName("quota_reset_date")] string? QuotaResetDate,
    [property: JsonPropertyName("limited_user_quotas")] CopilotFreeQuotas? LimitedUserQuotas,
    [property: JsonPropertyName("monthly_quotas")] CopilotFreeQuotas? MonthlyQuotas,
    [property: JsonPropertyName("limited_user_reset_date")] string? LimitedUserResetDate);

public sealed record CopilotQuotaSnapshots(
    [property: JsonPropertyName("premium_interactions")] CopilotQuotaSnapshot? PremiumInteractions,
    [property: JsonPropertyName("chat")] CopilotQuotaSnapshot? Chat);

public sealed record CopilotQuotaSnapshot(
    [property: JsonPropertyName("percent_remaining")] double? PercentRemaining);

public sealed record CopilotFreeQuotas(
    [property: JsonPropertyName("chat")] double? Chat,
    [property: JsonPropertyName("completions")] double? Completions);
