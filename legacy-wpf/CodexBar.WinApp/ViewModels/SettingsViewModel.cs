using CodexBar.Core.Settings;
using CodexBar.Core.Updates;
using CodexBar.Core.Paths;
using CodexBar.Core.Models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodexBar.WinApp.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private bool codexEnabled;
    private bool claudeEnabled;
    private bool cursorEnabled;
    private bool geminiEnabled;
    private bool mergeTrayIcon;
    private bool showUsageAsUsed;
    private bool dockOverviewNearTaskbar;
    private bool launchAtStartup;
    private bool checkForUpdatesAutomatically;
    private int refreshMinutes;
    private string codexSource = string.Empty;
    private string claudeSource = string.Empty;
    private string cursorSource = string.Empty;
    private string geminiSource = string.Empty;
    private string? claudeManualCookieHeader;
    private string? cursorManualCookieHeader;

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
        codexEnabled = settings.CodexEnabled;
        claudeEnabled = settings.ClaudeEnabled;
        cursorEnabled = settings.CursorEnabled;
        geminiEnabled = settings.GeminiEnabled;
        mergeTrayIcon = settings.MergeTrayIcon;
        showUsageAsUsed = settings.ShowUsageAsUsed;
        dockOverviewNearTaskbar = settings.DockOverviewNearTaskbar;
        launchAtStartup = settings.LaunchAtStartup;
        checkForUpdatesAutomatically = settings.CheckForUpdatesAutomatically;
        refreshMinutes = settings.RefreshMinutes;
        codexSource = settings.CodexSource;
        claudeSource = settings.ClaudeSource;
        cursorSource = settings.CursorSource;
        geminiSource = settings.GeminiSource;
        claudeManualCookieHeader = settings.ClaudeManualCookieHeader;
        cursorManualCookieHeader = settings.CursorManualCookieHeader;
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

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CodexEnabled { get => codexEnabled; set => SetField(ref codexEnabled, value); }
    public bool ClaudeEnabled { get => claudeEnabled; set => SetField(ref claudeEnabled, value); }
    public bool CursorEnabled { get => cursorEnabled; set => SetField(ref cursorEnabled, value); }
    public bool GeminiEnabled { get => geminiEnabled; set => SetField(ref geminiEnabled, value); }
    public bool MergeTrayIcon { get => mergeTrayIcon; set => SetField(ref mergeTrayIcon, value); }
    public bool ShowUsageAsUsed { get => showUsageAsUsed; set => SetField(ref showUsageAsUsed, value); }
    public bool DockOverviewNearTaskbar { get => dockOverviewNearTaskbar; set => SetField(ref dockOverviewNearTaskbar, value); }
    public bool LaunchAtStartup { get => launchAtStartup; set => SetField(ref launchAtStartup, value); }
    public bool CheckForUpdatesAutomatically { get => checkForUpdatesAutomatically; set => SetField(ref checkForUpdatesAutomatically, value); }
    public int RefreshMinutes { get => refreshMinutes; set => SetField(ref refreshMinutes, value); }
    public string CodexSource { get => codexSource; set => SetField(ref codexSource, value); }
    public string ClaudeSource { get => claudeSource; set => SetField(ref claudeSource, value); }
    public string CursorSource { get => cursorSource; set => SetField(ref cursorSource, value); }
    public string GeminiSource { get => geminiSource; set => SetField(ref geminiSource, value); }
    public string? ClaudeManualCookieHeader { get => claudeManualCookieHeader; set => SetField(ref claudeManualCookieHeader, value); }
    public string? CursorManualCookieHeader { get => cursorManualCookieHeader; set => SetField(ref cursorManualCookieHeader, value); }
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
        CheckForUpdatesAutomatically,
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

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
