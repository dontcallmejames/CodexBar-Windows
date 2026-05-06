using CodexBar.Core.Models;

namespace CodexBar.WinApp.ViewModels;

public sealed record DockedOverviewRow(string ProviderName, string PercentText, string ResetText, bool IsStale);

public sealed class DockedOverviewViewModel
{
    public DockedOverviewViewModel(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed, DateTimeOffset now)
    {
        Rows = snapshots.Select(snapshot =>
        {
            var window = snapshot.Windows.FirstOrDefault();
            var percent = window is null ? "--" : $"{Math.Round(showUsageAsUsed ? window.UsedPercent : window.PercentLeft):0}%";
            var reset = window?.ResetsAt is null ? string.Empty : $"Resets {FormatRelative(window.ResetsAt.Value, now)}";
            return new DockedOverviewRow(snapshot.DisplayName, percent, reset, snapshot.IsStale);
        }).ToArray();
    }

    public IReadOnlyList<DockedOverviewRow> Rows { get; }

    private static string FormatRelative(DateTimeOffset target, DateTimeOffset now)
    {
        var delta = target - now;
        if (delta.TotalMinutes < 1)
        {
            return "now";
        }

        if (delta.TotalHours < 1)
        {
            return $"in {Math.Max(1, (int)Math.Floor(delta.TotalMinutes))}m";
        }

        if (delta.TotalDays < 1)
        {
            return $"in {(int)Math.Floor(delta.TotalHours)}h";
        }

        return $"in {(int)Math.Floor(delta.TotalDays)}d";
    }
}
