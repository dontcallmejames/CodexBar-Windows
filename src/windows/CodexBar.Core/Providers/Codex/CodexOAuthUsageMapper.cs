using System.Text.Json;
using CodexBar.Core.Models;

namespace CodexBar.Core.Providers.Codex;

public static class CodexOAuthUsageMapper
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public static UsageSnapshot Map(CodexOAuthUsageResponse response, DateTimeOffset updatedAt)
    {
        var windows = new List<RateWindow>(2);
        AddWindow(windows, "primary", response.PrimaryWindow);
        AddWindow(windows, "secondary", response.SecondaryWindow);

        return new UsageSnapshot(
            UsageProvider.Codex,
            "Codex",
            updatedAt,
            windows,
            response.Account?.Email,
            response.Account?.PlanType,
            response.Credits?.HasCredits == true ? response.Credits.Balance : null,
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
            window.ResetsAt is null ? null : DateTimeOffset.FromUnixTimeSeconds(window.ResetsAt.Value),
            window.LimitWindowSeconds is null ? null : window.LimitWindowSeconds.Value / 60));
    }

    private static string TitleForWindow(int? seconds) =>
        seconds switch
        {
            18_000 => "5-hour",
            604_800 => "Weekly",
            _ => "Usage"
        };
}
