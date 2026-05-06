using CodexBar.Core.Models;

namespace CodexBar.Core.Refresh;

public sealed class SnapshotStore
{
    private readonly object gate = new();
    private readonly Dictionary<UsageProvider, UsageSnapshot> snapshots = new();

    public void Set(UsageSnapshot snapshot)
    {
        lock (gate)
        {
            snapshots[snapshot.Provider] = snapshot;
        }
    }

    public UsageSnapshot? Get(UsageProvider provider)
    {
        lock (gate)
        {
            return snapshots.TryGetValue(provider, out var snapshot) ? snapshot : null;
        }
    }

    public IReadOnlyList<UsageSnapshot> All()
    {
        lock (gate)
        {
            return snapshots.Values.OrderBy(snapshot => snapshot.Provider).ToArray();
        }
    }
}
