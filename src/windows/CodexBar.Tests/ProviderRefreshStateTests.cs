using CodexBar.Core.Models;
using CodexBar.Core.Refresh;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class ProviderRefreshStateTests
{
    [TestMethod]
    public void NewProvider_HasNoLastSuccess_AndIsDue()
    {
        var registry = new ProviderRefreshStateRegistry(() => DateTimeOffset.UnixEpoch);
        var state = registry.Get(UsageProvider.Codex);
        Assert.IsNull(state.LastSuccess);
        Assert.IsTrue(state.IsDue(DateTimeOffset.UnixEpoch));
    }

    [TestMethod]
    public void RecordSuccess_ResetsFailures_AndUpdatesTimestamp()
    {
        var clock = DateTimeOffset.Parse("2026-05-14T12:00:00Z");
        var registry = new ProviderRefreshStateRegistry(() => clock);
        registry.RecordFailure(UsageProvider.Codex, retryAfter: null);
        registry.RecordSuccess(UsageProvider.Codex);
        var state = registry.Get(UsageProvider.Codex);
        Assert.AreEqual(0, state.ConsecutiveFailures);
        Assert.AreEqual(clock, state.LastSuccess);
    }

    [TestMethod]
    public void RecordFailure_BlocksNextRefreshForBackoffWindow()
    {
        var clock = DateTimeOffset.Parse("2026-05-14T12:00:00Z");
        var registry = new ProviderRefreshStateRegistry(() => clock);
        registry.RecordFailure(UsageProvider.Codex, retryAfter: TimeSpan.FromMinutes(2));
        var state = registry.Get(UsageProvider.Codex);
        Assert.IsFalse(state.IsDue(clock.AddSeconds(30)));
        Assert.IsTrue(state.IsDue(clock.AddMinutes(3)));
    }
}
