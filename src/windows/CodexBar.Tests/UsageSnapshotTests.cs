using CodexBar.Core.Models;

namespace CodexBar.Tests;

[TestClass]
public sealed class UsageSnapshotTests
{
    [TestMethod]
    public void RequiresAuthentication_SetsAuthStateStaleAndMessage()
    {
        var snapshot = UsageSnapshot.RequiresAuthentication(UsageProvider.Claude, "Claude", "Sign in again.");

        Assert.AreEqual(UsageProvider.Claude, snapshot.Provider);
        Assert.AreEqual("Claude", snapshot.DisplayName);
        Assert.AreEqual(AuthState.RequiresAuthentication, snapshot.AuthState);
        Assert.IsTrue(snapshot.IsStale);
        Assert.AreEqual(0, snapshot.Windows.Count);
        Assert.AreEqual("Sign in again.", snapshot.ErrorMessage);
        Assert.AreEqual("none", snapshot.SourceLabel);
    }

    [TestMethod]
    public void MissingCredentials_StaysAuthStateNone()
    {
        var snapshot = UsageSnapshot.MissingCredentials(UsageProvider.Codex, "Codex", "Not configured.");

        Assert.AreEqual(AuthState.None, snapshot.AuthState);
        Assert.IsTrue(snapshot.IsStale);
    }

    [TestMethod]
    public void WithExpressionOnTransientFailureKeepsAuthStateNone()
    {
        var good = new UsageSnapshot(
            UsageProvider.Codex, "Codex", DateTimeOffset.Now, Array.Empty<RateWindow>(),
            null, null, null, null, null, null, null, "test", null, false);

        var stale = good with { IsStale = true, ErrorMessage = "timeout" };

        Assert.AreEqual(AuthState.None, stale.AuthState);
    }
}
