using CodexBar.Core.Models;

namespace CodexBar.WinApp.ViewModels;

public sealed class PopoverViewModel
{
    public PopoverViewModel(IReadOnlyList<UsageSnapshot> snapshots, UsageProvider activeProvider, bool showUsageAsUsed)
    {
        Snapshots = snapshots;
        ActiveProvider = activeProvider;
        ShowUsageAsUsed = showUsageAsUsed;
        ActiveSnapshot = snapshots.FirstOrDefault(snapshot => snapshot.Provider == activeProvider) ?? snapshots.FirstOrDefault();
        Tabs = snapshots.Select(snapshot => new ProviderTabViewModel(
            snapshot.Provider,
            snapshot.DisplayName,
            FormatPercent(snapshot.Windows.FirstOrDefault(), showUsageAsUsed),
            snapshot.Provider == activeProvider,
            snapshot.IsStale)).ToArray();
    }

    public IReadOnlyList<UsageSnapshot> Snapshots { get; }
    public UsageProvider ActiveProvider { get; }
    public bool ShowUsageAsUsed { get; }
    public UsageSnapshot? ActiveSnapshot { get; }
    public IReadOnlyList<ProviderTabViewModel> Tabs { get; }

    private static string FormatPercent(RateWindow? window, bool showUsageAsUsed)
    {
        if (window is null)
        {
            return "--";
        }

        var value = showUsageAsUsed ? window.UsedPercent : window.PercentLeft;
        return $"{Math.Round(value):0}%";
    }
}
