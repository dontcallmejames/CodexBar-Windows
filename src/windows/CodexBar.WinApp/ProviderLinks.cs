using CodexBar.Core.Models;

namespace CodexBar.WinApp;

public static class ProviderLinks
{
    public static Uri DashboardUri(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => new Uri("https://claude.ai/settings/usage"),
            _ => new Uri("https://chatgpt.com/codex/settings/usage")
        };

    public static Uri StatusUri(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => new Uri("https://status.anthropic.com/"),
            _ => new Uri("https://status.openai.com/")
        };
}
