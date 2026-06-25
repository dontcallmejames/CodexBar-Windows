using CodexBar.Core.Models;

namespace CodexBar.Core.Providers;

public static class ProviderLinks
{
    public static Uri BugReportUri() =>
        new("https://github.com/dontcallmejames/CodexBar-Windows/issues/new?template=bug_report.yml");

    public static Uri ReleasesUri() =>
        new("https://github.com/dontcallmejames/CodexBar-Windows/releases");

    public static Uri SetupUri(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-claude.md"),
            UsageProvider.Cursor => new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-cursor.md"),
            UsageProvider.Gemini => new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-gemini.md"),
            UsageProvider.Copilot => new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-copilot.md"),
            UsageProvider.Antigravity => new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-antigravity.md"),
            _ => new Uri("https://github.com/dontcallmejames/CodexBar-Windows/blob/main/docs/windows-codex.md")
        };

    public static Uri DashboardUri(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => new Uri("https://claude.ai/settings/usage"),
            UsageProvider.Cursor => new Uri("https://cursor.com/settings"),
            UsageProvider.Gemini => new Uri("https://aistudio.google.com/usage"),
            UsageProvider.Copilot => new Uri("https://github.com/settings/copilot"),
            UsageProvider.Antigravity => new Uri("https://antigravity.google"),
            _ => new Uri("https://chatgpt.com/codex/settings/usage")
        };

    public static Uri StatusUri(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => new Uri("https://status.anthropic.com/"),
            UsageProvider.Cursor => new Uri("https://status.cursor.com/"),
            UsageProvider.Gemini => new Uri("https://status.cloud.google.com/"),
            UsageProvider.Copilot => new Uri("https://www.githubstatus.com/"),
            UsageProvider.Antigravity => new Uri("https://status.cloud.google.com/"),
            _ => new Uri("https://status.openai.com/")
        };

    public static Uri AddAccountUri(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Copilot => new Uri("https://github.com/copilot"),
            _ => DashboardUri(provider)
        };
}
