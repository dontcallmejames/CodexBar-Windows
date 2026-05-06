using CodexBar.Core.Paths;

namespace CodexBar.Tests;

[TestClass]
public sealed class WindowsAppPathsTests
{
    [TestMethod]
    public void CreatesExpectedAppDataPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(root, root);

        Assert.AreEqual(Path.Combine(root, "CodexBar", "config.json"), paths.SettingsFile);
        Assert.AreEqual(Path.Combine(root, "CodexBar", "Cache"), paths.CacheDirectory);
        Assert.AreEqual(Path.Combine(root, "CodexBar", "Logs"), paths.LogDirectory);
    }

    [TestMethod]
    public void ResolvesCodexAndClaudeCredentialPaths()
    {
        var home = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var paths = WindowsAppPaths.ForTest(home, home);

        Assert.AreEqual(Path.Combine(home, ".codex", "auth.json"), paths.CodexAuthJson(null));
        Assert.AreEqual(Path.Combine(home, ".claude", ".credentials.json"), paths.ClaudeCredentialsJson);
        Assert.AreEqual(Path.Combine(home, "custom-codex", "auth.json"), paths.CodexAuthJson(Path.Combine(home, "custom-codex")));
    }
}
