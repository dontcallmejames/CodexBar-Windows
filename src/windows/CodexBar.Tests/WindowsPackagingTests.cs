namespace CodexBar.Tests;

[TestClass]
public sealed class WindowsPackagingTests
{
    [TestMethod]
    public void WindowsPackageScriptExists()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "Scripts",
            "package-windows.ps1"));

        Assert.IsTrue(File.Exists(scriptPath), scriptPath);
        var script = File.ReadAllText(scriptPath);
        StringAssert.Contains(script, "dotnet publish");
        StringAssert.Contains(script, "Compress-Archive");
        StringAssert.Contains(script, "CodexBar-Windows");
    }
}
