using CodexBar.Core.Models;

namespace CodexBar.WinApp.ViewModels;

public sealed record TaskbarDockTileViewModel(
    UsageProvider Provider,
    string ProviderName,
    string PercentText,
    double ProgressPercent,
    string ProgressColor,
    bool IsStale,
    bool IsEmpty);

public sealed class TaskbarDockViewModel
{
    public TaskbarDockViewModel(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        Tiles = snapshots.Select(snapshot =>
        {
            var window = snapshot.Windows.FirstOrDefault();
            if (window is null)
            {
                return new TaskbarDockTileViewModel(
                    snapshot.Provider,
                    snapshot.DisplayName,
                    "--",
                    0,
                    "#B8B2C8",
                    true,
                    true);
            }

            var percent = Math.Round(showUsageAsUsed ? window.UsedPercent : window.PercentLeft);
            var suffix = showUsageAsUsed ? "used" : "left";
            return new TaskbarDockTileViewModel(
                snapshot.Provider,
                snapshot.DisplayName,
                $"{percent:0}% {suffix}",
                Math.Clamp(percent, 0, 100),
                ProgressColor(snapshot.Provider),
                snapshot.IsStale,
                false);
        }).ToArray();
    }

    public IReadOnlyList<TaskbarDockTileViewModel> Tiles { get; }

    public bool HasTiles => Tiles.Count > 0;

    private static string ProgressColor(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Codex => "#35D2C6",
            UsageProvider.Claude => "#FF8C42",
            UsageProvider.Cursor => "#7264B8",
            UsageProvider.Gemini => "#2F82FF",
            _ => "#35D2C6"
        };
}
