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
        CursorEnabled = settings.CursorEnabled;
        GeminiEnabled = settings.GeminiEnabled;
        MergeTrayIcon = settings.MergeTrayIcon;
        ShowUsageAsUsed = settings.ShowUsageAsUsed;
        DockOverviewNearTaskbar = settings.DockOverviewNearTaskbar;
        LaunchAtStartup = settings.LaunchAtStartup;
        RefreshMinutes = settings.RefreshMinutes;
        CodexSource = settings.CodexSource;
        ClaudeSource = settings.ClaudeSource;
        CursorSource = settings.CursorSource;
        GeminiSource = settings.GeminiSource;
        ClaudeManualCookieHeader = settings.ClaudeManualCookieHeader;
        CursorManualCookieHeader = settings.CursorManualCookieHeader;
        CodexCredentialPath = paths?.CodexAuthJson(null) ?? string.Empty;
        ClaudeCredentialPath = paths?.ClaudeCredentialsJson ?? string.Empty;
        GeminiCredentialPath = paths?.GeminiOAuthCredentialsJson ?? string.Empty;
        CodexAccountStatus = CredentialStatus(CodexCredentialPath);
        ClaudeAccountStatus = CredentialStatus(ClaudeCredentialPath);
        CursorAccountStatus = string.IsNullOrWhiteSpace(CursorManualCookieHeader) ? "Not connected" : "Connected";
        GeminiAccountStatus = CredentialStatus(GeminiCredentialPath);
    }

    public bool CodexEnabled { get; set; }
    public bool ClaudeEnabled { get; set; }
    public bool CursorEnabled { get; set; }
    public bool GeminiEnabled { get; set; }
    public bool MergeTrayIcon { get; set; }
    public bool ShowUsageAsUsed { get; set; }
    public bool DockOverviewNearTaskbar { get; set; }
    public bool LaunchAtStartup { get; set; }
    public int RefreshMinutes { get; set; }
    public string CodexSource { get; set; }
    public string ClaudeSource { get; set; }
    public string CursorSource { get; set; }
    public string GeminiSource { get; set; }
    public string? ClaudeManualCookieHeader { get; set; }
    public string? CursorManualCookieHeader { get; set; }
    public string CodexCredentialPath { get; }
    public string ClaudeCredentialPath { get; }
    public string GeminiCredentialPath { get; }
    public string CodexAccountStatus { get; }
    public string ClaudeAccountStatus { get; }
    public string CursorAccountStatus { get; }
    public string GeminiAccountStatus { get; }

    public AppSettings ToSettings() => new(
        CodexEnabled,
        ClaudeEnabled,
        CursorEnabled,
        GeminiEnabled,
        MergeTrayIcon,
        ShowUsageAsUsed,
        DockOverviewNearTaskbar,
        LaunchAtStartup,
        RefreshMinutes,
        CodexSource,
        ClaudeSource,
        CursorSource,
        GeminiSource,
        ClaudeManualCookieHeader,
        CursorManualCookieHeader);

    private static string CredentialStatus(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? "Not checked"
            : File.Exists(path) ? "Connected" : "Not connected";
}
