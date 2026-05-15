using CodexBar.Core.Models;
using CodexBar.Core.Tray;
using CodexBar.Tray;

namespace CodexBar.WinApp.Services;

public sealed class TrayController : IDisposable
{
    private readonly TrayIconHost host;

    public TrayController(TrayIconHost host)
    {
        this.host = host;
    }

    public void Apply(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        host.Update(SelectDisplay(snapshots, showUsageAsUsed));
    }

    /// <summary>
    /// Pure function: maps snapshots to a TrayDisplayModel.
    /// Exact port of App.BuildTrayDisplay. The showUsageAsUsed parameter is
    /// accepted for forward compatibility but the current logic always uses
    /// UsedPercent (not PercentLeft), matching the existing behavior.
    /// </summary>
    public static TrayDisplayModel SelectDisplay(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        var primary = snapshots
            .SelectMany(snapshot => snapshot.Windows)
            .OrderByDescending(window => window.UsedPercent)
            .FirstOrDefault();
        var percent = primary?.UsedPercent ?? 0;
        var stale = snapshots.Count == 0 || snapshots.Any(snapshot => snapshot.IsStale);
        return new TrayDisplayModel("CodexBar", percent, stale);
    }

    public void Dispose() => host.Dispose();
}
