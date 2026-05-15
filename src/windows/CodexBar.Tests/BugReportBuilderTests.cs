using CodexBar.Core.Models;
using CodexBar.Core.Settings;
using CodexBar.Core.Updates;
using CodexBar.WinApp;

namespace CodexBar.Tests;

[TestClass]
public sealed class BugReportBuilderTests
{
    [TestMethod]
    public void DiagnosticSummaryIncludesProviderStateAndLatestErrors()
    {
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Codex,
                "Codex",
                DateTimeOffset.Parse("2026-05-07T12:00:00Z"),
                new[] { new RateWindow("weekly", "Weekly", 25, DateTimeOffset.Parse("2026-05-08T12:00:00Z"), null) },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "oauth",
                null,
                false),
            UsageSnapshot.MissingCredentials(UsageProvider.Gemini, "Gemini", "Gemini CLI OAuth credentials were not found.")
        };

        var summary = BugReportBuilder.BuildDiagnosticSummary(
            AppSettings.Default with { CursorEnabled = false, DockOverviewNearTaskbar = true },
            snapshots,
            appVersion: "0.25-test",
            osDescription: "Windows 11 test");

        StringAssert.Contains(summary, "App version: 0.25-test");
        StringAssert.Contains(summary, "OS: Windows 11 test");
        StringAssert.Contains(summary, "Taskbar dock enabled: True");
        StringAssert.Contains(summary, "- Codex: enabled, fresh, 1 usage window");
        StringAssert.Contains(summary, "- Cursor: disabled");
        StringAssert.Contains(summary, "- Gemini: enabled, stale, 0 usage windows, latest error: Gemini CLI OAuth credentials were not found.");
    }

    [TestMethod]
    public void DiagnosticSummaryIncludesLatestTestCredentialFailure()
    {
        var snapshots = new[]
        {
            UsageSnapshot.MissingCredentials(
                UsageProvider.Cursor,
                "Cursor",
                "Cursor cookie header was not found. Add it in Settings.")
        };

        var summary = BugReportBuilder.BuildDiagnosticSummary(
            AppSettings.Default,
            snapshots,
            appVersion: "0.25-test",
            osDescription: "Windows 11 test");

        StringAssert.Contains(summary, "- Cursor: enabled, stale, 0 usage windows, latest error: Cursor cookie header was not found. Add it in Settings.");
    }

    [TestMethod]
    public void DiagnosticSummaryDoesNotIncludeManualCookieHeaders()
    {
        var settings = AppSettings.Default with
        {
            ClaudeManualCookieHeader = "sessionKey=super-secret-claude",
            CursorManualCookieHeader = "WorkosCursorSessionToken=super-secret-cursor"
        };

        var summary = BugReportBuilder.BuildDiagnosticSummary(
            settings,
            Array.Empty<UsageSnapshot>(),
            appVersion: "0.25-test",
            osDescription: "Windows 11 test");

        Assert.IsFalse(summary.Contains("super-secret-claude", StringComparison.Ordinal));
        Assert.IsFalse(summary.Contains("super-secret-cursor", StringComparison.Ordinal));
        Assert.IsFalse(summary.Contains("sessionKey=", StringComparison.Ordinal));
        Assert.IsFalse(summary.Contains("WorkosCursorSessionToken=", StringComparison.Ordinal));
        StringAssert.Contains(summary, "No tokens, cookies, OAuth files, or credential contents are included.");
    }

    [TestMethod]
    public void DiagnosticSummaryIncludesUpdateStatusWhenAvailable()
    {
        var summary = BugReportBuilder.BuildDiagnosticSummary(
            AppSettings.Default,
            Array.Empty<UsageSnapshot>(),
            appVersion: "0.25-test",
            osDescription: "Windows 11 test",
            updateStatus: UpdateCheckResult.Available(
                "v0.25.0-preview.3",
                new Uri("https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.3")));

        StringAssert.Contains(summary, "Update status: update available (v0.25.0-preview.3)");
    }
}
