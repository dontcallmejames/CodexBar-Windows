using CodexBar.Core.Settings;

namespace CodexBar.WinApp.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel(AppSettings settings)
    {
        CodexEnabled = settings.CodexEnabled;
        ClaudeEnabled = settings.ClaudeEnabled;
        MergeTrayIcon = settings.MergeTrayIcon;
        ShowUsageAsUsed = settings.ShowUsageAsUsed;
        DockOverviewNearTaskbar = settings.DockOverviewNearTaskbar;
        RefreshMinutes = settings.RefreshMinutes;
        CodexSource = settings.CodexSource;
        ClaudeSource = settings.ClaudeSource;
        ClaudeManualCookieHeader = settings.ClaudeManualCookieHeader;
    }

    public bool CodexEnabled { get; set; }
    public bool ClaudeEnabled { get; set; }
    public bool MergeTrayIcon { get; set; }
    public bool ShowUsageAsUsed { get; set; }
    public bool DockOverviewNearTaskbar { get; set; }
    public int RefreshMinutes { get; set; }
    public string CodexSource { get; set; }
    public string ClaudeSource { get; set; }
    public string? ClaudeManualCookieHeader { get; set; }

    public AppSettings ToSettings() => new(
        CodexEnabled,
        ClaudeEnabled,
        MergeTrayIcon,
        ShowUsageAsUsed,
        DockOverviewNearTaskbar,
        RefreshMinutes,
        CodexSource,
        ClaudeSource,
        ClaudeManualCookieHeader);
}
