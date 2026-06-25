using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Antigravity;

public static class AntigravityUsageMapper
{
    private const StringComparison OIC = StringComparison.OrdinalIgnoreCase;

    public static UsageSnapshot Map(string method, JsonElement root, DateTimeOffset updatedAt)
    {
        var payload = Unwrap(root);
        return method == "RetrieveUserQuotaSummary"
            ? BuildSummary(payload, updatedAt)
            : BuildUserStatus(payload, updatedAt);
    }

    /// <summary>
    /// Reads the plan tier and account email from a GetUserStatus response. RetrieveUserQuotaSummary
    /// carries quota but no identity, so the provider fetches GetUserStatus separately to fill these.
    /// </summary>
    public static (string? Plan, string? Email) ReadIdentity(JsonElement userStatusRoot)
    {
        var payload = Unwrap(userStatusRoot);
        return (Clean(UserStatusPlan(payload)), Clean(UserStatusEmail(payload)));
    }

    // The language server wraps responses in a Connect "response" envelope on some builds and
    // returns the bare payload on others. Descend into it when present.
    private static JsonElement Unwrap(JsonElement root) =>
        root.TryGetProperty("response", out var inner) && inner.ValueKind == JsonValueKind.Object
            ? inner
            : root;

    private sealed record Bucket(string Identity, double? RemainingFraction, DateTimeOffset? ResetsAt, bool Disabled);

    // RetrieveUserQuotaSummary: groups (e.g. "Gemini Models", "Claude and GPT models"), each with
    // weekly + 5-hour window buckets. Render one lane per bucket, labelled "<group> · <window>".
    private static UsageSnapshot BuildSummary(JsonElement payload, DateTimeOffset updatedAt)
    {
        var windows = new List<RateWindow>();
        if (payload.TryGetProperty("groups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in groups.EnumerateArray())
            {
                var groupLabel = ShortGroupLabel(ReadString(group, "displayName"));
                if (!group.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var bucket in buckets.EnumerateArray())
                {
                    if (ReadBool(bucket, "disabled"))
                    {
                        continue;
                    }

                    var fraction = ReadDouble(bucket, "remainingFraction");
                    if (fraction is null)
                    {
                        continue;
                    }

                    var bucketName = ReadString(bucket, "displayName") ?? ReadString(bucket, "bucketId") ?? "Limit";
                    var title = string.IsNullOrEmpty(groupLabel) ? bucketName : $"{groupLabel} · {bucketName}";
                    windows.Add(new RateWindow(
                        ReadString(bucket, "bucketId") ?? title,
                        title,
                        UsedPercent(fraction.Value),
                        ReadResetTime(bucket, "resetTime"),
                        null));
                }
            }
        }

        return Snapshot(windows, plan: null, email: null, updatedAt);
    }

    // GetUserStatus / GetCommandModelConfigs: per-model quota under cascadeModelConfigData.
    private static UsageSnapshot BuildUserStatus(JsonElement payload, DateTimeOffset updatedAt)
    {
        var list = UserStatusBuckets(payload)
            .Where(b => !b.Disabled && b.RemainingFraction is not null)
            .ToArray();
        var windows = new List<RateWindow>(3);
        AddLane(windows, list, "claude", "Claude", s => s.Contains("claude", OIC));
        AddLane(windows, list, "gemini-pro", "Gemini Pro", s => s.Contains("gemini", OIC) && s.Contains("pro", OIC));
        AddLane(windows, list, "gemini-flash", "Gemini Flash", s => s.Contains("gemini", OIC) && s.Contains("flash", OIC));

        return Snapshot(windows, UserStatusPlan(payload), UserStatusEmail(payload), updatedAt);
    }

    private static UsageSnapshot Snapshot(
        IReadOnlyList<RateWindow> windows, string? plan, string? email, DateTimeOffset updatedAt) =>
        new(
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

    private static double UsedPercent(double remainingFraction) =>
        Math.Round(Math.Clamp((1.0 - remainingFraction) * 100.0, 0.0, 100.0), 2);

    // "Gemini Models" -> "Gemini"; "Claude and GPT models" -> "Claude and GPT".
    private static string ShortGroupLabel(string? displayName)
    {
        var name = (displayName ?? string.Empty).Trim();
        return Regex.Replace(name, @"\s+models?$", string.Empty, RegexOptions.IgnoreCase).Trim();
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
            UsedPercent(bucket.RemainingFraction.Value),
            bucket.ResetsAt,
            null));
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

        // Older builds expose userTier.preferredName / .name; current builds use
        // planStatus.planInfo.planName (e.g. "Pro"). Try all, most-specific first.
        if (userStatus.TryGetProperty("userTier", out var tier) && tier.ValueKind == JsonValueKind.Object)
        {
            var name = ReadString(tier, "preferredName") ?? ReadString(tier, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        if (userStatus.TryGetProperty("planStatus", out var planStatus) &&
            planStatus.TryGetProperty("planInfo", out var planInfo))
        {
            var plan = ReadString(planInfo, "preferredName") ?? ReadString(planInfo, "planName");
            if (!string.IsNullOrWhiteSpace(plan))
            {
                return plan;
            }
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

        // Current builds put the address at userStatus.email.
        return root.TryGetProperty("userStatus", out var userStatus)
            ? ReadString(userStatus, "email") ?? ReadString(userStatus, "accountEmail")
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
