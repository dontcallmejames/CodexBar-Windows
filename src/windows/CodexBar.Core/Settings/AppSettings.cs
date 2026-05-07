namespace CodexBar.Core.Settings;

public sealed record AppSettings(
    bool CodexEnabled,
    bool ClaudeEnabled,
    bool MergeTrayIcon,
    bool ShowUsageAsUsed,
    bool DockOverviewNearTaskbar,
    bool LaunchAtStartup,
    int RefreshMinutes,
    string CodexSource,
    string ClaudeSource,
    string? ClaudeManualCookieHeader)
{
    public static AppSettings Default { get; } = new(
        CodexEnabled: true,
        ClaudeEnabled: true,
        MergeTrayIcon: true,
        ShowUsageAsUsed: true,
        DockOverviewNearTaskbar: false,
        LaunchAtStartup: false,
        RefreshMinutes: 5,
        CodexSource: "auto",
        ClaudeSource: "auto",
        ClaudeManualCookieHeader: null);
}
