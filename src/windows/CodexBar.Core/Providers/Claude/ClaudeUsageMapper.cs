using System.Globalization;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Claude;

public static class ClaudeUsageMapper
{
    private const int SessionMinutes = 5 * 60;
    private const int WeeklyMinutes = 7 * 24 * 60;

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static UsageSnapshot Map(
        ClaudeUsageResponse response,
        DateTimeOffset updatedAt,
        string sourceLabel,
        ClaudeOAuthCredentials? credentials = null)
    {
        var windows = new List<RateWindow>(5);
        AddWindow(windows, "session", "Session", response.FiveHour, SessionMinutes);
        AddWindow(windows, "weekly", "Weekly", response.SevenDay ?? response.SevenDayOAuthApps, WeeklyMinutes);
        AddWindow(windows, "sonnet", "Sonnet", response.SevenDaySonnet, WeeklyMinutes);
        AddWindow(windows, "opus", "Opus", response.SevenDayOpus, WeeklyMinutes);
        AddProductWindow(
            windows,
            response,
            "designs",
            "Designs",
            [
                "seven_day_design",
                "seven_day_claude_design",
                "claude_design",
                "design",
                "seven_day_omelette",
                "omelette",
                "omelette_promotional"
            ]);
        AddProductWindow(
            windows,
            response,
            "routines",
            "Daily Routines",
            [
                "seven_day_routines",
                "seven_day_claude_routines",
                "claude_routines",
                "routines",
                "routine",
                "seven_day_cowork",
                "cowork"
            ]);

        if (windows.Count == 0)
        {
            AddWindow(windows, "weekly", "Weekly", response.SevenDayOAuthApps, WeeklyMinutes);
        }

        return new UsageSnapshot(
            UsageProvider.Claude,
            "Claude",
            updatedAt,
            windows,
            Clean(response.Account?.Email) ?? Clean(response.Account?.EmailAddress),
            Clean(response.Account?.SubscriptionType)
                ?? Clean(response.Account?.SubscriptionTypeCamel)
                ?? Clean(credentials?.SubscriptionType)
                ?? PlanFromTier(response.Account?.RateLimitTier ?? credentials?.RateLimitTier),
            null,
            UsedCostUsd(response.ExtraUsage),
            null,
            null,
            null,
            sourceLabel,
            null,
            false);
    }

    private static void AddWindow(
        ICollection<RateWindow> windows,
        string id,
        string title,
        ClaudeUsageWindow? window,
        int windowMinutes)
    {
        var usedPercent = window?.Utilization ?? window?.UsedPercent;
        if (usedPercent is null)
        {
            return;
        }

        windows.Add(new RateWindow(
            id,
            title,
            usedPercent.Value,
            ParseReset(window?.ResetsAt),
            windowMinutes));
    }

    private static void AddProductWindow(
        ICollection<RateWindow> windows,
        ClaudeUsageResponse response,
        string id,
        string title,
        IReadOnlyList<string> aliases)
    {
        var (window, wasPresent) = DecodeAliasedWindow(response, aliases);
        if (window is null && !wasPresent)
        {
            return;
        }

        windows.Add(new RateWindow(
            id,
            title,
            Math.Clamp(window?.Utilization ?? window?.UsedPercent ?? 0, 0, 100),
            ParseReset(window?.ResetsAt),
            WeeklyMinutes));
    }

    private static (ClaudeUsageWindow? Window, bool WasPresent) DecodeAliasedWindow(
        ClaudeUsageResponse response,
        IReadOnlyList<string> aliases)
    {
        if (response.ExtensionData is null)
        {
            return (null, false);
        }

        var sawNull = false;
        foreach (var alias in aliases)
        {
            if (!response.ExtensionData.TryGetValue(alias, out var value))
            {
                continue;
            }

            if (value.ValueKind is JsonValueKind.Null)
            {
                sawNull = true;
                continue;
            }

            try
            {
                return (value.Deserialize<ClaudeUsageWindow>(JsonOptions), true);
            }
            catch (JsonException)
            {
                sawNull = true;
            }
        }

        return (null, sawNull);
    }

    private static DateTimeOffset? ParseReset(JsonElement? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.String => ParseResetString(value.Value.GetString()),
            JsonValueKind.Number when value.Value.TryGetInt64(out var seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds),
            _ => null
        };
    }

    private static DateTimeOffset? ParseResetString(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    private static decimal? UsedCostUsd(ClaudeExtraUsage? extra)
    {
        if (extra is null || extra.IsEnabled == false)
        {
            return null;
        }

        return extra.UsedUsd ?? extra.UsedCredits / 100m;
    }

    private static string? PlanFromTier(string? rateLimitTier)
    {
        var tier = Clean(rateLimitTier)?.ToLowerInvariant();
        if (tier is null)
        {
            return null;
        }

        if (tier.Contains("max", StringComparison.Ordinal))
        {
            return "Max";
        }

        if (tier.Contains("pro", StringComparison.Ordinal))
        {
            return "Pro";
        }

        if (tier.Contains("team", StringComparison.Ordinal))
        {
            return "Team";
        }

        if (tier.Contains("enterprise", StringComparison.Ordinal))
        {
            return "Enterprise";
        }

        if (tier.Contains("ultra", StringComparison.Ordinal))
        {
            return "Ultra";
        }

        return null;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
