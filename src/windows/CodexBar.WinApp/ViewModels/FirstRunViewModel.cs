using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace CodexBar.WinApp.ViewModels;

public sealed class FirstRunViewModel : INotifyPropertyChanged
{
    private readonly AppSettings originalSettings;
    private bool codexEnabled;
    private bool claudeEnabled;
    private bool cursorEnabled;
    private bool geminiEnabled;

    public FirstRunViewModel(
        AppSettings settings,
        IAppPaths? paths = null,
        IReadOnlyList<UsageSnapshot>? snapshots = null)
    {
        originalSettings = settings;
        var byProvider = (snapshots ?? Array.Empty<UsageSnapshot>())
            .ToDictionary(snapshot => snapshot.Provider);
        codexEnabled = settings.CodexEnabled;
        claudeEnabled = settings.ClaudeEnabled;
        cursorEnabled = settings.CursorEnabled;
        geminiEnabled = settings.GeminiEnabled;

        var codexCredentialPath = paths?.CodexAuthJson(null) ?? string.Empty;
        var claudeCredentialPath = paths?.ClaudeCredentialsJson ?? string.Empty;
        var geminiCredentialPath = paths?.GeminiOAuthCredentialsJson ?? string.Empty;
        (CodexAccountStatus, CodexAccountDetail) = ProviderStatus(
            UsageProvider.Codex,
            CodexEnabled,
            CredentialStatus(codexCredentialPath),
            codexCredentialPath,
            byProvider);
        (ClaudeAccountStatus, ClaudeAccountDetail) = ProviderStatus(
            UsageProvider.Claude,
            ClaudeEnabled,
            CredentialStatus(claudeCredentialPath),
            claudeCredentialPath,
            byProvider);
        (CursorAccountStatus, CursorAccountDetail) = ProviderStatus(
            UsageProvider.Cursor,
            CursorEnabled,
            string.IsNullOrWhiteSpace(settings.CursorManualCookieHeader) ? "Not connected" : "Connected",
            "Manual cookie header",
            byProvider);
        (GeminiAccountStatus, GeminiAccountDetail) = ProviderStatus(
            UsageProvider.Gemini,
            GeminiEnabled,
            CredentialStatus(geminiCredentialPath),
            geminiCredentialPath,
            byProvider);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CodexEnabled { get => codexEnabled; set => SetField(ref codexEnabled, value); }
    public bool ClaudeEnabled { get => claudeEnabled; set => SetField(ref claudeEnabled, value); }
    public bool CursorEnabled { get => cursorEnabled; set => SetField(ref cursorEnabled, value); }
    public bool GeminiEnabled { get => geminiEnabled; set => SetField(ref geminiEnabled, value); }
    public string CodexAccountStatus { get; }
    public string ClaudeAccountStatus { get; }
    public string CursorAccountStatus { get; }
    public string GeminiAccountStatus { get; }
    public string CodexAccountDetail { get; }
    public string ClaudeAccountDetail { get; }
    public string CursorAccountDetail { get; }
    public string GeminiAccountDetail { get; }

    public AppSettings ToSettings() => originalSettings with
    {
        CodexEnabled = CodexEnabled,
        ClaudeEnabled = ClaudeEnabled,
        CursorEnabled = CursorEnabled,
        GeminiEnabled = GeminiEnabled
    };

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
