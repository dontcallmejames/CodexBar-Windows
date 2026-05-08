using CodexBar.Core.Settings;
using CodexBar.Core.Paths;
using CodexBar.Core.Models;
using System.IO;

namespace CodexBar.WinApp.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel(
        AppSettings settings,
        IAppPaths? paths = null,
        IReadOnlyList<UsageSnapshot>? snapshots = null,
        AppVersionInfo? versionInfo = null,
        UpdateCheckResult? updateStatus = null)
    {
        versionInfo ??= AppVersionInfo.Current;
        var byProvider = (snapshots ?? Array.Empty<UsageSnapshot>())
            .ToDictionary(snapshot => snapshot.Provider);
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
        (CodexAccountStatus, CodexAccountDetail) = ProviderStatus(
            UsageProvider.Codex,
            CodexEnabled,
            CredentialStatus(CodexCredentialPath),
            CodexCredentialPath,
            byProvider);
        (ClaudeAccountStatus, ClaudeAccountDetail) = ProviderStatus(
            UsageProvider.Claude,
            ClaudeEnabled,
            CredentialStatus(ClaudeCredentialPath),
            ClaudeCredentialPath,
            byProvider);
        (CursorAccountStatus, CursorAccountDetail) = ProviderStatus(
            UsageProvider.Cursor,
            CursorEnabled,
            string.IsNullOrWhiteSpace(CursorManualCookieHeader) ? "Not connected" : "Connected",
            "Manual cookie header",
            byProvider);
        (GeminiAccountStatus, GeminiAccountDetail) = ProviderStatus(
            UsageProvider.Gemini,
            GeminiEnabled,
            CredentialStatus(GeminiCredentialPath),
            GeminiCredentialPath,
            byProvider);
        CurrentVersionText = $"Version {versionInfo.CurrentTag}";
        LatestVersionText = updateStatus?.LatestTag is { Length: > 0 }
            ? $"Latest {updateStatus.LatestTag}"
            : "Latest not checked";
        UpdateStatusText = updateStatus?.StatusText ?? "Update status: not checked";
        UpdateActionText = updateStatus switch
        {
            { UpdateAvailable: true } => "Open Release...",
            { ErrorMessage: not null } => "Open Releases...",
            _ => "Check for Updates..."
        };
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
    public string CodexAccountDetail { get; }
    public string ClaudeAccountDetail { get; }
    public string CursorAccountDetail { get; }
    public string GeminiAccountDetail { get; }
    public string CurrentVersionText { get; }
    public string LatestVersionText { get; }
    public string UpdateStatusText { get; }
    public string UpdateActionText { get; }

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

    private static (string Status, string Detail) ProviderStatus(
        UsageProvider provider,
        bool enabled,
        string credentialStatus,
        string fallbackDetail,
        IReadOnlyDictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        if (!enabled)
        {
            return ("Disabled", "Provider is disabled.");
        }

        if (!snapshots.TryGetValue(provider, out var snapshot))
        {
            return (credentialStatus, string.IsNullOrWhiteSpace(fallbackDetail) ? "No credential path available." : fallbackDetail);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
        {
            return ("Needs attention", snapshot.ErrorMessage);
        }

        if (snapshot.IsStale)
        {
            return ("Needs attention", "Last refresh did not complete successfully.");
        }

        if (snapshot.Windows.Count == 0)
        {
            return ("No usage yet", $"Credentials were found, but {snapshot.DisplayName} did not return usage windows yet.");
        }

        return ("Connected", "Usage data refreshed successfully.");
    }
}
