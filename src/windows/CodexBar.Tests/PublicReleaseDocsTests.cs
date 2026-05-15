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
        StringAssert.Contains(readme, "## Provider Support Matrix");
        StringAssert.Contains(readme, "| Provider | Credential source | Usage status | Notes |");
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
        StringAssert.Contains(signingGuide, "CODEXBAR_SIGNING_CERTIFICATE_BASE64");
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
        StringAssert.Contains(windowsDoc, "Open Release");
        StringAssert.Contains(windowsDoc, "Check for updates automatically");
        StringAssert.Contains(windowsDoc, "Known limitations");
        StringAssert.Contains(windowsDoc, "Gemini");
        StringAssert.Contains(windowsDoc, "Cursor");
    }

    [TestMethod]
    public void ReadmeMarksMacOSArtifactsAsLegacy()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            ".."));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));

        StringAssert.Contains(readme, "Legacy macOS sources");
        StringAssert.Contains(readme, "`legacy-macos/`");
        StringAssert.Contains(readme, "Package.swift");
        StringAssert.Contains(readme, "appcast.xml");
        StringAssert.Contains(readme, "Windows releases are built from `src/windows`");
        Assert.IsTrue(Directory.Exists(Path.Combine(repoRoot, "legacy-macos")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "legacy-macos", "Package.swift")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "legacy-macos", "appcast.xml")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "legacy-macos", "codexbar.png")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "legacy-macos", "docs", "RELEASING.md")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "legacy-macos", "bin", "install-codexbar-cli.sh")));
        Assert.IsTrue(File.Exists(Path.Combine(repoRoot, "legacy-macos", "bin", "docs-list")));
        Assert.IsFalse(File.Exists(Path.Combine(repoRoot, "Package.swift")));
        Assert.IsFalse(File.Exists(Path.Combine(repoRoot, "appcast.xml")));
        Assert.IsFalse(File.Exists(Path.Combine(repoRoot, "codexbar.png")));
        Assert.IsFalse(File.Exists(Path.Combine(repoRoot, "docs", "RELEASING.md")));
        Assert.IsFalse(Directory.Exists(Path.Combine(repoRoot, "bin")));
    }

    [TestMethod]
    public void MacOSReleaseScriptRequiresExplicitLegacyOptIn()
    {
        var script = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "legacy-macos",
            "Scripts",
            "release.sh")));

        StringAssert.Contains(script, "CODEXBAR_RUN_LEGACY_MACOS_RELEASE");
        StringAssert.Contains(script, "Legacy macOS release script");
        StringAssert.Contains(script, "SPARKLE_LIB");
    }

    [TestMethod]
    public void LegacySwiftAndUpstreamWorkflowsAreManualOnly()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            ".."));
        var legacyCi = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));
        var legacyCli = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "release-cli.yml"));
        var upstreamMonitor = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "upstream-monitor.yml"));

        StringAssert.Contains(legacyCi, "name: Legacy Swift CI");
        StringAssert.Contains(legacyCi, "workflow_dispatch:");
        StringAssert.Contains(legacyCi, "working-directory: legacy-macos");
        Assert.IsFalse(legacyCi.Contains("pull_request:", StringComparison.Ordinal));
        Assert.IsFalse(legacyCi.Contains("push:", StringComparison.Ordinal));

        StringAssert.Contains(legacyCli, "name: Legacy CLI Release");
        StringAssert.Contains(legacyCli, "workflow_dispatch:");
        StringAssert.Contains(legacyCli, "working-directory: legacy-macos");
        Assert.IsFalse(legacyCli.Contains("release:", StringComparison.Ordinal));

        StringAssert.Contains(upstreamMonitor, "name: Legacy Upstream Monitor");
        StringAssert.Contains(upstreamMonitor, "workflow_dispatch:");
        StringAssert.Contains(upstreamMonitor, "legacy-macos/Scripts/review_upstream.sh");
        StringAssert.Contains(upstreamMonitor, "legacy-macos/Scripts/analyze_quotio.sh");
        Assert.IsFalse(upstreamMonitor.Contains("schedule:", StringComparison.Ordinal));
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
