namespace CodexBar.Tests;

[TestClass]
public sealed class PublicReleaseDocsTests
{
    [TestMethod]
    public void ReadmePositionsWindowsAppAndPreservesUpstreamLicenseLink()
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
        // MIT requires preserving the upstream attribution; we keep one link to the original.
        StringAssert.Contains(readme, "https://github.com/steipete/CodexBar");
        StringAssert.Contains(readme, "Windows 11");
        StringAssert.Contains(readme, "Cursor");
        StringAssert.Contains(readme, "Gemini");
        StringAssert.Contains(readme, "Copilot");
        StringAssert.Contains(readme, "credentials stay on your machine");
        StringAssert.Contains(readme, "## Screenshot");
        StringAssert.Contains(readme, "## Provider Support Matrix");
        StringAssert.Contains(readme, "| Provider | Credential source | Status | Notes |");
        // Every provider with a setup doc must be linked from the README's setup section.
        // Copilot's link was missing once even though docs/windows-copilot.md existed.
        StringAssert.Contains(readme, "docs/windows-codex.md");
        StringAssert.Contains(readme, "docs/windows-claude.md");
        StringAssert.Contains(readme, "docs/windows-cursor.md");
        StringAssert.Contains(readme, "docs/windows-gemini.md");
        StringAssert.Contains(readme, "docs/windows-copilot.md");
        StringAssert.Contains(readme, "installer");
        StringAssert.Contains(readme, "portable zip");
        StringAssert.Contains(readme, "Open Release");
        StringAssert.Contains(readme, "checks GitHub Releases automatically");
        StringAssert.Contains(readme, "Test");
        StringAssert.Contains(readme, "Help");
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
        var signingGuide = File.ReadAllText(Path.Combine(repoRoot, "docs", "windows-signing.md"));

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
        StringAssert.Contains(releaseChecklist, "Windows signing");
        StringAssert.Contains(signingGuide, "Azure Trusted Signing");
        StringAssert.Contains(signingGuide, "TRUSTED_SIGNING_ENDPOINT");
        StringAssert.Contains(signingGuide, "TRUSTED_SIGNING_ACCOUNT_NAME");
        StringAssert.Contains(signingGuide, "TRUSTED_SIGNING_PROFILE_NAME");
        StringAssert.Contains(signingGuide, "AZURE_CLIENT_ID");
        StringAssert.Contains(signingGuide, "OIDC");
        StringAssert.Contains(signingGuide, "CODEXBAR_SIGNING_CERTIFICATE_PASSWORD");
        StringAssert.Contains(signingGuide, "signtool");
        StringAssert.Contains(signingGuide, "SmartScreen");
    }

    [TestMethod]
    public void WindowsDocsIncludeUpdateAndProviderTroubleshooting()
    {
        var windowsDoc = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "docs",
            "windows.md")));

        StringAssert.Contains(windowsDoc, "No usage yet");
        StringAssert.Contains(windowsDoc, "Install now");
        StringAssert.Contains(windowsDoc, "Check for updates automatically");
        StringAssert.Contains(windowsDoc, "Known limitations");
        StringAssert.Contains(windowsDoc, "Gemini");
        StringAssert.Contains(windowsDoc, "Cursor");

        var copilotDoc = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "docs",
            "windows-copilot.md")));
        StringAssert.Contains(copilotDoc, "gh auth login");
        StringAssert.Contains(copilotDoc, "copilot_internal/user");
    }

    [TestMethod]
    public void NoLegacyDirectoriesRemainAtRepoRoot()
    {
        // Legacy macOS Swift sources and the previous WPF shell have been removed from
        // the active tree. They still exist in git history but should not be in HEAD.
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            ".."));

        Assert.IsFalse(Directory.Exists(Path.Combine(repoRoot, "legacy-macos")));
        Assert.IsFalse(Directory.Exists(Path.Combine(repoRoot, "legacy-wpf")));
        Assert.IsFalse(File.Exists(Path.Combine(repoRoot, "Package.swift")));
        Assert.IsFalse(File.Exists(Path.Combine(repoRoot, "appcast.xml")));
        Assert.IsFalse(Directory.Exists(Path.Combine(repoRoot, "bin")));
    }

    [TestMethod]
    public void RepositoryKeepsWorkflowAndShellScriptLineEndingsStable()
    {
        var attributes = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            ".gitattributes")));

        StringAssert.Contains(attributes, "*.yml text eol=lf");
        StringAssert.Contains(attributes, "*.yaml text eol=lf");
        StringAssert.Contains(attributes, "*.sh text eol=lf");
    }
}
