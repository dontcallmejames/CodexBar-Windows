using System.Globalization;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Gemini;

public static class GeminiUsageMapper
{
    public static UsageSnapshot Map(
        JsonElement loadCodeAssist,
        JsonElement quota,
        string? email,
        DateTimeOffset updatedAt)
    {
        var windows = new List<RateWindow>(2);
        var buckets = EnumerateQuotaBuckets(quota).ToArray();

        AddFamilyWindow(windows, buckets, "pro", "Pro models");
        AddFamilyWindow(windows, buckets, "flash", "Flash models");

        return new UsageSnapshot(
            UsageProvider.Gemini,
            "Gemini",
            updatedAt,
            windows,
            Clean(email),
            Plan(loadCodeAssist),
            null,
            null,
            null,
            null,
            null,
            "oauth",
            windows.Count == 0 ? "No usage data" : null,
            windows.Count == 0);
    }

    private static void AddFamilyWindow(
        ICollection<RateWindow> windows,
        IEnumerable<JsonElement> buckets,
        string family,
        string title)
    {
        var bucket = buckets
            .Where(item => ReadString(item, "modelId")?.Contains(family, StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => new
            {
                Bucket = item,
                RemainingFraction = ReadDouble(item, "remainingFraction")
            })
            .Where(item => item.RemainingFraction is not null)
            .OrderBy(item => item.RemainingFraction)
            .FirstOrDefault();
        if (bucket?.RemainingFraction is null)
        {
            return;
        }

        windows.Add(new RateWindow(
            family,
            title,
            Math.Round(Math.Clamp((1.0 - bucket.RemainingFraction.Value) * 100.0, 0.0, 100.0), 2),
            ReadDateTime(bucket.Bucket, "resetTime"),
            null));
    }

    private static IEnumerable<JsonElement> EnumerateQuotaBuckets(JsonElement quota)
    {
        foreach (var propertyName in new[] { "quota", "quotas", "quotaBuckets", "usage", "buckets" })
        {
            if (!quota.TryGetProperty(propertyName, out var buckets) || buckets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var bucket in buckets.EnumerateArray())
            {
                if (bucket.ValueKind == JsonValueKind.Object)
                {
                    yield return bucket;
                }
            }
        }
    }

    private static string? Plan(JsonElement loadCodeAssist)
    {
        var tier = Clean(ReadNestedString(loadCodeAssist, "paidTier", "id"))
            ?? Clean(ReadNestedString(loadCodeAssist, "currentTier", "id"))
            ?? Clean(ReadNestedString(loadCodeAssist, "tier", "id"));

        return tier switch
        {
            "standard-tier" => "Paid",
            "free-tier" when ContainsHdClaim(loadCodeAssist) => "Workspace",
            "free-tier" => "Free",
            "legacy-tier" => "Legacy",
            _ => null
        };
    }

    private static bool ContainsHdClaim(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, "hd", StringComparison.OrdinalIgnoreCase) ||
                        ContainsHdClaim(property.Value))
                    {
                        return true;
                    }
                }

                return false;
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(ContainsHdClaim);
            case JsonValueKind.String:
                return string.Equals(element.GetString(), "hd", StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
    }

    private static DateTimeOffset? ReadDateTime(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string? ReadNestedString(JsonElement element, string propertyName, string childName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object
            ? ReadString(property, childName)
            : null;

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

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
