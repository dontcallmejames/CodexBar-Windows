namespace CodexBar.Tests;

[TestClass]
public sealed class PackagingScriptTests
{
    [TestMethod]
    public void WindowsPackageScriptWritesSha256Checksum()
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
        var script = File.ReadAllText(scriptPath);

        StringAssert.Contains(script, "Get-FileHash");
        StringAssert.Contains(script, ".sha256");
    }
}
