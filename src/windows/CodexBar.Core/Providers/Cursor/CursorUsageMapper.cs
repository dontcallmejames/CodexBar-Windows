using System.Globalization;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Cursor;

public static class CursorUsageMapper
{
    public static UsageSnapshot Map(JsonElement usage, JsonElement? account, DateTimeOffset updatedAt)
    {
        var windows = new List<RateWindow>();
        var reset = ReadDateTime(usage, "billingCycleEnd") ?? ReadDateTime(usage, "currentPeriodEnd");

        AddPercentWindow(windows, usage, "includedUsage", "includedUsageLimit", "included_plan", "Included plan", reset);
        AddPercentWindow(windows, usage, "onDemandUsage", "onDemandUsageLimit", "on_demand", "On-demand", reset);

        return new UsageSnapshot(
            UsageProvider.Cursor,
            "Cursor",
            updatedAt,
            windows,
            ReadString(account, "email"),
            null,
            null,
            ReadDecimal(usage, "onDemandUsage"),
            null,
            null,
            null,
            "manual cookie",
            windows.Count == 0 ? "No usage data" : null,
            windows.Count == 0);
    }

    private static void AddPercentWindow(
        ICollection<RateWindow> windows,
        JsonElement usage,
        string usedProperty,
        string limitProperty,
        string id,
        string title,
        DateTimeOffset? reset)
    {
        var used = ReadDouble(usage, usedProperty);
        var limit = ReadDouble(usage, limitProperty);
        if (used is null || limit is null or <= 0)
        {
            return;
        }

        var usedPercent = Math.Clamp((used.Value / limit.Value) * 100, 0, 100);
        windows.Add(new RateWindow(id, title, usedPercent, reset, null));
    }

    private static string? ReadString(JsonElement? root, string property)
    {
        if (root is null || !root.Value.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static decimal? ReadDecimal(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private static double? ReadDouble(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }

        return value.ValueKind == JsonValueKind.String &&
            double.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    private static DateTimeOffset? ReadDateTime(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
                ? parsed
                : null;
    }
}
