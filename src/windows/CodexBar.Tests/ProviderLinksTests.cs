using CodexBar.Core.Models;
using CodexBar.WinApp;

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
}
