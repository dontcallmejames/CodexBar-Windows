using CodexBar.Core.Settings;
using CodexBar.Core.Paths;
using System.IO;

namespace CodexBar.WinApp.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel(AppSettings settings, IAppPaths? paths = null)
    {
        CodexEnabled = settings.CodexEnabled;
        ClaudeEnabled = settings.ClaudeEnabled;
        MergeTrayIcon = settings.MergeTrayIcon;
        ShowUsageAsUsed = settings.ShowUsageAsUsed;
        DockOverviewNearTaskbar = settings.DockOverviewNearTaskbar;
        LaunchAtStartup = settings.LaunchAtStartup;
        RefreshMinutes = settings.RefreshMinutes;
        CodexSource = settings.CodexSource;
        ClaudeSource = settings.ClaudeSource;
        ClaudeManualCookieHeader = settings.ClaudeManualCookieHeader;
        CodexCredentialPath = paths?.CodexAuthJson(null) ?? string.Empty;
        ClaudeCredentialPath = paths?.ClaudeCredentialsJson ?? string.Empty;
        CodexAccountStatus = CredentialStatus(CodexCredentialPath);
        ClaudeAccountStatus = CredentialStatus(ClaudeCredentialPath);
    }

    public bool CodexEnabled { get; set; }
    public bool ClaudeEnabled { get; set; }
    public bool MergeTrayIcon { get; set; }
    public bool ShowUsageAsUsed { get; set; }
    public bool DockOverviewNearTaskbar { get; set; }
    public bool LaunchAtStartup { get; set; }
    public int RefreshMinutes { get; set; }
    public string CodexSource { get; set; }
    public string ClaudeSource { get; set; }
    public string? ClaudeManualCookieHeader { get; set; }
    public string CodexCredentialPath { get; }
    public string ClaudeCredentialPath { get; }
    public string CodexAccountStatus { get; }
    public string ClaudeAccountStatus { get; }

    public AppSettings ToSettings() => new(
        CodexEnabled,
        ClaudeEnabled,
        MergeTrayIcon,
        ShowUsageAsUsed,
        DockOverviewNearTaskbar,
        LaunchAtStartup,
        RefreshMinutes,
        CodexSource,
        ClaudeSource,
        ClaudeManualCookieHeader);

    private static string CredentialStatus(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? "Not checked"
            : File.Exists(path) ? "Connected" : "Not connected";
}
