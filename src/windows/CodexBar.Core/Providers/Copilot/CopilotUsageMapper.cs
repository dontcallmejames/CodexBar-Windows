using System.Globalization;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Copilot;

public static class CopilotUsageMapper
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static UsageSnapshot Map(CopilotUserResponse response, DateTimeOffset updatedAt)
    {
        var windows = new List<RateWindow>(2);
        var plan = response.CopilotPlan;
        var isFreeTier = string.Equals(plan, "free", StringComparison.OrdinalIgnoreCase);

        if (isFreeTier)
        {
            // Free tier: limited_user_quotas reports remaining count, monthly_quotas reports the limit.
            AddFreeWindow(windows, "chat", "Chat",
                response.LimitedUserQuotas?.Chat,
                response.MonthlyQuotas?.Chat,
                ParseDate(response.LimitedUserResetDate));
            AddFreeWindow(windows, "completions", "Completions",
                response.LimitedUserQuotas?.Completions,
                response.MonthlyQuotas?.Completions,
                ParseDate(response.LimitedUserResetDate));
        }
        else
        {
            // Paid tiers: quota_snapshots gives percent_remaining directly.
            AddPaidWindow(windows, "premium", "Premium",
                response.QuotaSnapshots?.PremiumInteractions?.PercentRemaining,
                ParseDate(response.QuotaResetDate));
            AddPaidWindow(windows, "chat", "Chat",
                response.QuotaSnapshots?.Chat?.PercentRemaining,
                ParseDate(response.QuotaResetDate));
        }

        return new UsageSnapshot(
            UsageProvider.Copilot,
            "Copilot",
            updatedAt,
            windows,
            null,
            FormatPlan(plan),
            null,
            null,
            null,
            null,
            null,
            "gh-cli",
            null,
            false);
    }

    private static void AddPaidWindow(ICollection<RateWindow> windows, string id, string title, double? percentRemaining, DateTimeOffset? resetsAt)
    {
        if (percentRemaining is null)
        {
            return;
        }

        var used = Math.Clamp(100.0 - percentRemaining.Value, 0.0, 100.0);
        windows.Add(new RateWindow(id, title, used, resetsAt, null));
    }

    private static void AddFreeWindow(ICollection<RateWindow> windows, string id, string title, double? remaining, double? limit, DateTimeOffset? resetsAt)
    {
        if (remaining is null || limit is null || limit.Value <= 0)
        {
            return;
        }

        var used = limit.Value - remaining.Value;
        var percent = Math.Clamp(used / limit.Value * 100.0, 0.0, 100.0);
        windows.Add(new RateWindow(id, title, percent, resetsAt, null));
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? FormatPlan(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan))
        {
            return null;
        }

        // Title-case for display: "pro" -> "Pro", "business" -> "Business", etc.
        return char.ToUpperInvariant(plan[0]) + plan.Substring(1);
    }
}
