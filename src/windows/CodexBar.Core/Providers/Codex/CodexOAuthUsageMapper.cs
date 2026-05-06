using System.Globalization;
using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Codex;

public static class CodexOAuthUsageMapper
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static UsageSnapshot Map(CodexOAuthUsageResponse response, DateTimeOffset updatedAt)
    {
        var windows = new List<RateWindow>(2);
        AddWindow(windows, "primary", response.RateLimit?.PrimaryWindow ?? response.PrimaryWindow);
        AddWindow(windows, "secondary", response.RateLimit?.SecondaryWindow ?? response.SecondaryWindow);

        return new UsageSnapshot(
            UsageProvider.Codex,
            "Codex",
            updatedAt,
            windows,
            response.Account?.Email,
            response.Account?.PlanType ?? response.PlanType,
            response.Credits?.HasCredits == true ? ReadCreditBalance(response.Credits.Balance) : null,
            null,
            null,
            null,
            null,
            "oauth",
            null,
            false);
    }

    private static void AddWindow(ICollection<RateWindow> windows, string id, CodexOAuthWindow? window)
    {
        if (window is null)
        {
            return;
        }

        windows.Add(new RateWindow(
            id,
            TitleForWindow(window.LimitWindowSeconds),
            window.UsedPercent,
            WindowReset(window),
            window.LimitWindowSeconds is null ? null : window.LimitWindowSeconds.Value / 60));
    }

    private static DateTimeOffset? WindowReset(CodexOAuthWindow window)
    {
        var reset = window.ResetsAt ?? window.ResetAt;
        return reset is null ? null : DateTimeOffset.FromUnixTimeSeconds(reset.Value);
    }

    private static decimal? ReadCreditBalance(JsonElement? balance)
    {
        if (balance is null)
        {
            return null;
        }

        return balance.Value.ValueKind switch
        {
            JsonValueKind.Number when balance.Value.TryGetDecimal(out var value) => value,
            JsonValueKind.String when decimal.TryParse(
                balance.Value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value) => value,
            _ => null
        };
    }

    private static string TitleForWindow(int? seconds) =>
        seconds switch
        {
            18_000 => "5-hour",
            604_800 => "Weekly",
            _ => "Usage"
        };
}
