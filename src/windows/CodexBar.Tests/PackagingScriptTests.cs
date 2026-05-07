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

    [TestMethod]
    public void WindowsWorkflowPackagesOnlyTagBuilds()
    {
        var workflowPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            ".github",
            "workflows",
            "windows.yml"));
        var workflow = File.ReadAllText(workflowPath);

        StringAssert.Contains(workflow, "tags:");
        StringAssert.Contains(workflow, "v*");
        StringAssert.Contains(workflow, "if: startsWith(github.ref, 'refs/tags/')");
    }
}
