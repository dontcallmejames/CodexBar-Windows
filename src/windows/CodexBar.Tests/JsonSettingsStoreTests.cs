using CodexBar.Core.Settings;

namespace CodexBar.Tests;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    [TestMethod]
    public async Task MissingFileReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(settings.CodexEnabled);
        Assert.IsTrue(settings.ClaudeEnabled);
        Assert.IsTrue(settings.MergeTrayIcon);
        Assert.AreEqual(5, settings.RefreshMinutes);
    }

    [TestMethod]
    public async Task SavesAndLoadsManualClaudeCookieHeader()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        var store = new JsonSettingsStore(path);
        var settings = AppSettings.Default with { ClaudeManualCookieHeader = "sessionKey=abc" };

        await store.SaveAsync(settings, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual("sessionKey=abc", loaded.ClaudeManualCookieHeader);
    }
}
