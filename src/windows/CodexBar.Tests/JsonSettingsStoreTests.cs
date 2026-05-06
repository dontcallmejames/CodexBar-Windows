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

    [TestMethod]
    public async Task EmptyJsonReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{}", CancellationToken.None);
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(settings.CodexEnabled);
        Assert.IsTrue(settings.ClaudeEnabled);
        Assert.IsTrue(settings.MergeTrayIcon);
        Assert.AreEqual(5, settings.RefreshMinutes);
        Assert.AreEqual("auto", settings.CodexSource);
        Assert.AreEqual("auto", settings.ClaudeSource);
    }

    [TestMethod]
    public async Task PartialJsonPreservesProvidedValuesAndDefaultsOmittedValues()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """{ "refreshMinutes": 1, "claudeManualCookieHeader": "sessionKey=abc" }""",
            CancellationToken.None);
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.IsTrue(settings.CodexEnabled);
        Assert.IsTrue(settings.ClaudeEnabled);
        Assert.IsTrue(settings.MergeTrayIcon);
        Assert.AreEqual(1, settings.RefreshMinutes);
        Assert.AreEqual("auto", settings.CodexSource);
        Assert.AreEqual("auto", settings.ClaudeSource);
        Assert.AreEqual("sessionKey=abc", settings.ClaudeManualCookieHeader);
    }

    [TestMethod]
    public async Task InvalidSourceAndRefreshValuesNormalizeToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            """{ "refreshMinutes": 0, "codexSource": null, "claudeSource": "" }""",
            CancellationToken.None);
        var store = new JsonSettingsStore(path);

        var settings = await store.LoadAsync(CancellationToken.None);

        Assert.AreEqual(5, settings.RefreshMinutes);
        Assert.AreEqual("auto", settings.CodexSource);
        Assert.AreEqual("auto", settings.ClaudeSource);
    }
}
