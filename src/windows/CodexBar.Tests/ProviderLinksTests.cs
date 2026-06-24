using CodexBar.Core.Models;
using CodexBar.Core.Providers;

namespace CodexBar.Tests;

[TestClass]
public sealed class ProviderLinksTests
{
    [TestMethod]
    public void ResolvesProviderDashboardAndStatusUrls()
    {
        Assert.AreEqual(new Uri("https://chatgpt.com/codex/settings/usage"), ProviderLinks.DashboardUri(UsageProvider.Codex));
        Assert.AreEqual(new Uri("https://status.openai.com/"), ProviderLinks.StatusUri(UsageProvider.Codex));
        Assert.AreEqual(new Uri("https://claude.ai/settings/usage"), ProviderLinks.DashboardUri(UsageProvider.Claude));
        Assert.AreEqual(new Uri("https://status.anthropic.com/"), ProviderLinks.StatusUri(UsageProvider.Claude));
    }

    [TestMethod]
    public void ResolvesBugReportUrlToGithubIssueForm()
    {
        Assert.AreEqual(
            new Uri("https://github.com/dontcallmejames/CodexBar-Windows/issues/new?template=bug_report.yml"),
            ProviderLinks.BugReportUri());
    }

    [TestMethod]
    public void ResolvesReleasesUrlForUpdates()
    {
        Assert.AreEqual(
            new Uri("https://github.com/dontcallmejames/CodexBar-Windows/releases"),
            ProviderLinks.ReleasesUri());
    }

    [TestMethod]
    public void ResolvesProviderSetupDocs()
    {
        Assert.AreEqual(
            new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-codex.md"),
            ProviderLinks.SetupUri(UsageProvider.Codex));
        Assert.AreEqual(
            new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-claude.md"),
            ProviderLinks.SetupUri(UsageProvider.Claude));
        Assert.AreEqual(
            new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-cursor.md"),
            ProviderLinks.SetupUri(UsageProvider.Cursor));
        Assert.AreEqual(
            new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-gemini.md"),
            ProviderLinks.SetupUri(UsageProvider.Gemini));
        Assert.AreEqual(
            new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-copilot.md"),
            ProviderLinks.SetupUri(UsageProvider.Copilot));
    }

    [TestMethod]
    public void SetupUriReturnsDistinctNonNullUriForAllProviders()
    {
        var providers = new[]
        {
            UsageProvider.Codex,
            UsageProvider.Claude,
            UsageProvider.Cursor,
            UsageProvider.Gemini,
            UsageProvider.Copilot
        };

        var uris = providers.Select(ProviderLinks.SetupUri).ToArray();

        Assert.IsTrue(uris.All(uri => uri is not null));
        Assert.AreEqual(providers.Length, uris.Distinct().Count(), "each provider must map to a distinct setup doc");
    }
}
