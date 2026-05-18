namespace CodexBar.Core.Settings;

public sealed record AppSettings(
    bool CodexEnabled,
    bool ClaudeEnabled,
    bool CursorEnabled,
    bool GeminiEnabled,
    bool MergeTrayIcon,
    bool ShowUsageAsUsed,
    bool DockOverviewNearTaskbar,
    bool LaunchAtStartup,
    bool CheckForUpdatesAutomatically,
    int RefreshMinutes,
    string CodexSource,
    string ClaudeSource,
    string CursorSource,
    string GeminiSource,
    string? ClaudeManualCookieHeader,
    string? CursorManualCookieHeader,
    string GlobalHotkey,
    bool EnableGlobalHotkey)
{
    public static AppSettings Default { get; } = new(
        CodexEnabled: true,
        ClaudeEnabled: true,
        CursorEnabled: true,
        GeminiEnabled: true,
        MergeTrayIcon: true,
        ShowUsageAsUsed: true,
        DockOverviewNearTaskbar: false,
        LaunchAtStartup: false,
        CheckForUpdatesAutomatically: true,
        RefreshMinutes: 5,
        CodexSource: "auto",
        ClaudeSource: "auto",
        CursorSource: "auto",
        GeminiSource: "auto",
        ClaudeManualCookieHeader: null,
        CursorManualCookieHeader: null,
        GlobalHotkey: "Ctrl+Alt+U",
        EnableGlobalHotkey: true);
}
