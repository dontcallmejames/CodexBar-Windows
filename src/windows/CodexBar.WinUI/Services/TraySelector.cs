using System.Collections.Generic;
using System.Linq;
using CodexBar.Core.Models;
using CodexBar.Core.Tray;

namespace CodexBar.WinUI.Services;

public static class TraySelector
{
    public static TrayDisplayModel Build(IReadOnlyList<UsageSnapshot> snapshots)
    {
        var primary = snapshots
            .SelectMany(snapshot => snapshot.Windows)
            .OrderByDescending(window => window.UsedPercent)
            .FirstOrDefault();
        var percent = primary?.UsedPercent ?? 0;
        var stale = snapshots.Count == 0 || snapshots.Any(snapshot => snapshot.IsStale);
        return new TrayDisplayModel("CodexBar", percent, stale);
    }
}
