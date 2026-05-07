using CodexBar.Core.Models;

namespace CodexBar.WinApp;

public static class ProviderLinks
{
    public static Uri BugReportUri() =>
        new("https://github.com/dontcallmejames/CodexBar-Windows/issues/new?template=bug_report.yml");

    public static Uri DashboardUri(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => new Uri("https://claude.ai/settings/usage"),
            UsageProvider.Cursor => new Uri("https://cursor.com/settings"),
            UsageProvider.Gemini => new Uri("https://aistudio.google.com/usage"),
            _ => new Uri("https://chatgpt.com/codex/settings/usage")
        };

    public static Uri StatusUri(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => new Uri("https://status.anthropic.com/"),
            UsageProvider.Cursor => new Uri("https://status.cursor.com/"),
            UsageProvider.Gemini => new Uri("https://status.cloud.google.com/"),
            _ => new Uri("https://status.openai.com/")
        };
}
