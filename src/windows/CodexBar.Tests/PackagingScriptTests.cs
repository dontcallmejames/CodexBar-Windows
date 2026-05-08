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
        StringAssert.Contains(script, "BUILD_NUMBER");
        StringAssert.Contains(script, "WINDOWS_PREVIEW_NUMBER");
        StringAssert.Contains(script, "-p:InformationalVersion=$version");
        StringAssert.Contains(script, "-p:IncludeSourceRevisionInInformationalVersion=false");
        StringAssert.Contains(script, "-p:BuildNumber=$buildNumber");
        StringAssert.Contains(script, "-p:WindowsPreviewNumber=$windowsPreviewNumber");
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

    [TestMethod]
    public void WindowsWorkflowPublishesPreviewReleaseAssetsForTags()
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

        StringAssert.Contains(workflow, "contents: write");
        StringAssert.Contains(workflow, "softprops/action-gh-release");
        StringAssert.Contains(workflow, "prerelease: true");
        StringAssert.Contains(workflow, "dist/windows/*.zip");
        StringAssert.Contains(workflow, "dist/windows/*.zip.sha256");
    }

    [TestMethod]
    public void WindowsInstallerScriptBuildsInnoSetupInstallerAndChecksum()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            ".."));
        var scriptPath = Path.Combine(repoRoot, "Scripts", "package-windows-installer.ps1");
        var innoPath = Path.Combine(repoRoot, "installer", "windows", "CodexBar.iss");

        Assert.IsTrue(File.Exists(scriptPath), scriptPath);
        Assert.IsTrue(File.Exists(innoPath), innoPath);
        var script = File.ReadAllText(scriptPath);
        var inno = File.ReadAllText(innoPath);

        StringAssert.Contains(script, "ISCC.exe");
        StringAssert.Contains(script, "package-windows.ps1");
        StringAssert.Contains(script, "Get-FileHash");
        StringAssert.Contains(script, ".installer.exe.sha256");
        StringAssert.Contains(inno, "CodexBar for Windows");
        StringAssert.Contains(inno, "CodexBar.WinApp.exe");
        StringAssert.Contains(inno, "{group}\\CodexBar");
    }

    [TestMethod]
    public void WindowsWorkflowPublishesInstallerAssetsForTags()
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

        StringAssert.Contains(workflow, "choco install innosetup");
        StringAssert.Contains(workflow, "package-windows-installer.ps1");
        StringAssert.Contains(workflow, "dist/windows/*.installer.exe");
        StringAssert.Contains(workflow, "dist/windows/*.installer.exe.sha256");
    }
}
