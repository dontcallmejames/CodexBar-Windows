namespace CodexBar.Tests;

[TestClass]
public sealed class PublicReleaseDocsTests
{
    [TestMethod]
    public void ReadmePositionsWindowsPreviewAndCreditsOriginalProject()
    {
        var readme = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "README.md")));

        StringAssert.Contains(readme, "CodexBar for Windows");
        StringAssert.Contains(readme, "https://github.com/steipete/CodexBar");
        StringAssert.Contains(readme, "Windows 11");
        StringAssert.Contains(readme, "Cursor");
        StringAssert.Contains(readme, "Gemini");
        StringAssert.Contains(readme, "credentials stay on your machine");
        StringAssert.Contains(readme, "## Screenshot");
        StringAssert.Contains(readme, "installer");
        StringAssert.Contains(readme, "portable zip");
        StringAssert.Contains(readme, "Test");
        StringAssert.Contains(readme, "Help");
    }

    [TestMethod]
    public void AboutWindowCreditsOriginalProject()
    {
        var about = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "AboutWindow.xaml")));

        StringAssert.Contains(about, "Inspired by Peter Steinberger's CodexBar");
        StringAssert.Contains(about, "https://github.com/steipete/CodexBar");
        StringAssert.Contains(about, "Version");
        StringAssert.Contains(about, "Release channel");
    }

    [TestMethod]
    public void PublicRepoIncludesIssueTemplatesAndReleaseChecklist()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            ".."));
        var bugTemplate = File.ReadAllText(Path.Combine(repoRoot, ".github", "ISSUE_TEMPLATE", "bug_report.yml"));
        var providerTemplate = File.ReadAllText(Path.Combine(repoRoot, ".github", "ISSUE_TEMPLATE", "provider_request.yml"));
        var pullRequestTemplate = File.ReadAllText(Path.Combine(repoRoot, ".github", "pull_request_template.md"));
        var releaseChecklist = File.ReadAllText(Path.Combine(repoRoot, "docs", "windows-release-checklist.md"));

        StringAssert.Contains(bugTemplate, "Provider");
        StringAssert.Contains(bugTemplate, "Portable zip");
        StringAssert.Contains(bugTemplate, "Diagnostic summary");
        StringAssert.Contains(bugTemplate, "Report a Bug");
        StringAssert.Contains(providerTemplate, "Credential source");
        StringAssert.Contains(providerTemplate, "Usage data");
        StringAssert.Contains(pullRequestTemplate, "Windows tests");
        StringAssert.Contains(releaseChecklist, "v0.25.0-preview.1");
        StringAssert.Contains(releaseChecklist, "GitHub Release");
        StringAssert.Contains(releaseChecklist, "CodexBar-Windows");
        StringAssert.Contains(releaseChecklist, ".installer.exe");
    }
}
