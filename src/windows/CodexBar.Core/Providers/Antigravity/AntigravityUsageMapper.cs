using System.Globalization;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Antigravity;

public static class AntigravityUsageMapper
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public static UsageSnapshot Map(string method, JsonElement root, DateTimeOffset updatedAt) =>
        method == "RetrieveUserQuotaSummary"
            ? Build(QuotaSummaryBuckets(root), plan: null, email: null, updatedAt)
            : Build(UserStatusBuckets(root), UserStatusPlan(root), UserStatusEmail(root), updatedAt);

    private sealed record Bucket(string Identity, double? RemainingFraction, DateTimeOffset? ResetsAt, bool Disabled);

    private static UsageSnapshot Build(IEnumerable<Bucket> buckets, string? plan, string? email, DateTimeOffset updatedAt)
    {
        var list = buckets.Where(b => !b.Disabled && b.RemainingFraction is not null).ToArray();
        var windows = new List<RateWindow>(3);
        AddLane(windows, list, "claude", "Claude", s => s.Contains("claude", OIC));
        AddLane(windows, list, "gemini-pro", "Gemini Pro", s => s.Contains("gemini", OIC) && s.Contains("pro", OIC));
        AddLane(windows, list, "gemini-flash", "Gemini Flash", s => s.Contains("gemini", OIC) && s.Contains("flash", OIC));

        return new UsageSnapshot(
            UsageProvider.Antigravity,
            "Antigravity",
            updatedAt,
            windows,
            Clean(email),
            Clean(plan),
            null, null, null, null, null,
            "local",
            windows.Count == 0 ? "Limits not available" : null,
            windows.Count == 0);
    }

    private static void AddLane(
        ICollection<RateWindow> windows,
        IEnumerable<Bucket> buckets,
        string id,
        string title,
        Func<string, bool> matches)
    {
        var bucket = buckets
            .Where(b => matches(b.Identity))
            .OrderBy(b => b.RemainingFraction)
            .FirstOrDefault();
        if (bucket?.RemainingFraction is null)
        {
            return;
        }

        windows.Add(new RateWindow(
            id,
            title,
            Math.Round(Math.Clamp((1.0 - bucket.RemainingFraction.Value) * 100.0, 0.0, 100.0), 2),
            bucket.ResetsAt,
            null));
    }

    private static IEnumerable<Bucket> QuotaSummaryBuckets(JsonElement root)
    {
        if (!root.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var group in groups.EnumerateArray())
        {
            if (!group.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var b in buckets.EnumerateArray())
            {
                var identity = $"{ReadString(b, "bucketId")} {ReadString(b, "displayName")}";
                yield return new Bucket(
                    identity,
                    ReadDouble(b, "remainingFraction"),
                    ReadResetTime(b, "resetTime"),
                    ReadBool(b, "disabled"));
            }
        }
    }

    private static IEnumerable<Bucket> UserStatusBuckets(JsonElement root)
    {
        if (!root.TryGetProperty("userStatus", out var userStatus) ||
            !userStatus.TryGetProperty("cascadeModelConfigData", out var data) ||
            !data.TryGetProperty("clientModelConfigs", out var configs) ||
            configs.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var config in configs.EnumerateArray())
        {
            var model = config.TryGetProperty("modelOrAlias", out var moa) ? ReadString(moa, "model") : null;
            var identity = $"{ReadString(config, "label")} {model}";
            double? remaining = null;
            DateTimeOffset? resets = null;
            if (config.TryGetProperty("quotaInfo", out var quotaInfo) && quotaInfo.ValueKind == JsonValueKind.Object)
            {
                remaining = ReadDouble(quotaInfo, "remainingFraction");
                resets = ReadResetTime(quotaInfo, "resetTime");
            }

            yield return new Bucket(identity, remaining, resets, Disabled: false);
        }
    }

    private static string? UserStatusPlan(JsonElement root)
    {
        if (!root.TryGetProperty("userStatus", out var userStatus))
        {
            return ReadString(root, "planName");
        }

        if (userStatus.TryGetProperty("userTier", out var tier) && tier.ValueKind == JsonValueKind.Object)
        {
            var name = ReadString(tier, "preferredName");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        if (userStatus.TryGetProperty("planStatus", out var planStatus) &&
            planStatus.TryGetProperty("planInfo", out var planInfo))
        {
            return ReadString(planInfo, "preferredName");
        }

        return ReadString(root, "planName");
    }

    private static string? UserStatusEmail(JsonElement root)
    {
        var email = ReadString(root, "accountEmail");
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        return root.TryGetProperty("userStatus", out var userStatus)
            ? ReadString(userStatus, "accountEmail")
            : null;
    }

    private static DateTimeOffset? ReadResetTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToUniversalTime()
                : null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static double? ReadDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetDouble(out var value)
            ? value
            : null;

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.True;

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
