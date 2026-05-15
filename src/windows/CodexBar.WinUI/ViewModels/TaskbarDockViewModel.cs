using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CodexBar.Core.Models;

namespace CodexBar.WinUI.ViewModels;

public sealed record TaskbarDockTileViewModel(
    UsageProvider Provider,
    string ProviderName,
    string PercentText,
    double ProgressPercent,
    bool IsStale,
    bool IsEmpty);

public sealed class TaskbarDockViewModel
{
    public ObservableCollection<TaskbarDockTileViewModel> Tiles { get; } = new();

    public bool HasTiles => Tiles.Count > 0;

    public TaskbarDockViewModel(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        foreach (var tile in BuildTiles(snapshots, showUsageAsUsed))
        {
            Tiles.Add(tile);
        }
    }

    /// <summary>
    /// Reconcile this VM's tile collection in place from the given snapshots. The window's
    /// XAML binds to Tiles once; mutating the collection lets the ItemsRepeater react via
    /// INotifyCollectionChanged without us swapping the ItemsSource imperatively.
    /// </summary>
    public void ReconcileFrom(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        Tiles.Clear();
        foreach (var tile in BuildTiles(snapshots, showUsageAsUsed))
        {
            Tiles.Add(tile);
        }
    }

    private static IEnumerable<TaskbarDockTileViewModel> BuildTiles(
        IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        return snapshots.Select(snapshot =>
        {
            var window = snapshot.Windows.FirstOrDefault();
            if (window is null)
            {
                return new TaskbarDockTileViewModel(
                    snapshot.Provider,
                    snapshot.DisplayName,
                    "--",
                    0,
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
                snapshot.IsStale,
                false);
        });
    }
}
