using System.Collections.Generic;
using CodexBar.Core.Models;

namespace CodexBar.Core.Settings;

public sealed record AppSettings(
    bool CodexEnabled,
    bool ClaudeEnabled,
    bool CursorEnabled,
    bool GeminiEnabled,
    bool CopilotEnabled,
    bool AntigravityEnabled,
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
    string CopilotSource,
    string AntigravitySource,
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
        // Copilot ships off by default — requires the user to run `gh auth login` first.
        CopilotEnabled: false,
        AntigravityEnabled: true,
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
        CopilotSource: "auto",
        AntigravitySource: "auto",
        ClaudeManualCookieHeader: null,
        CursorManualCookieHeader: null,
        GlobalHotkey: "Ctrl+Alt+U",
        EnableGlobalHotkey: true);

    /// <summary>
    /// The providers the user has enabled, in display order. Single source of truth for the
    /// menu/popover tab list (App.ResolveEnabledProviders) so a new provider can't be wired
    /// into one place and silently forgotten in another.
    /// </summary>
    public IReadOnlyList<UsageProvider> EnabledProviders()
    {
        var providers = new List<UsageProvider>(6);
        if (CodexEnabled) providers.Add(UsageProvider.Codex);
        if (ClaudeEnabled) providers.Add(UsageProvider.Claude);
        if (CursorEnabled) providers.Add(UsageProvider.Cursor);
        if (GeminiEnabled) providers.Add(UsageProvider.Gemini);
        if (CopilotEnabled) providers.Add(UsageProvider.Copilot);
        if (AntigravityEnabled) providers.Add(UsageProvider.Antigravity);
        return providers;
    }
}
