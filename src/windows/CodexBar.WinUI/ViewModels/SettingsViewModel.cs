using CodexBar.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodexBar.WinUI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    // Original settings kept so unchanged fields survive the round-trip.
    private readonly AppSettings originalSettings;

    [ObservableProperty] private bool codexEnabled;
    [ObservableProperty] private bool claudeEnabled;
    [ObservableProperty] private bool cursorEnabled;
    [ObservableProperty] private bool geminiEnabled;
    [ObservableProperty] private int refreshMinutes;
    [ObservableProperty] private bool dockOverviewNearTaskbar;
    [ObservableProperty] private bool launchAtStartup;
    [ObservableProperty] private bool checkForUpdatesAutomatically;
    [ObservableProperty] private bool showUsageAsUsed;
    [ObservableProperty] private string claudeManualCookieHeader = string.Empty;
    [ObservableProperty] private string cursorManualCookieHeader = string.Empty;

    public SettingsViewModel(AppSettings settings)
    {
        originalSettings = settings;
        codexEnabled = settings.CodexEnabled;
        claudeEnabled = settings.ClaudeEnabled;
        cursorEnabled = settings.CursorEnabled;
        geminiEnabled = settings.GeminiEnabled;
        refreshMinutes = settings.RefreshMinutes;
        dockOverviewNearTaskbar = settings.DockOverviewNearTaskbar;
        launchAtStartup = settings.LaunchAtStartup;
        checkForUpdatesAutomatically = settings.CheckForUpdatesAutomatically;
        showUsageAsUsed = settings.ShowUsageAsUsed;
        claudeManualCookieHeader = settings.ClaudeManualCookieHeader ?? string.Empty;
        cursorManualCookieHeader = settings.CursorManualCookieHeader ?? string.Empty;
    }

    public AppSettings ToSettings() => new(
        CodexEnabled,
        ClaudeEnabled,
        CursorEnabled,
        GeminiEnabled,
        originalSettings.MergeTrayIcon,
        ShowUsageAsUsed,
        DockOverviewNearTaskbar,
        LaunchAtStartup,
        CheckForUpdatesAutomatically,
        RefreshMinutes,
        originalSettings.CodexSource,
        originalSettings.ClaudeSource,
        originalSettings.CursorSource,
        originalSettings.GeminiSource,
        string.IsNullOrWhiteSpace(ClaudeManualCookieHeader) ? null : ClaudeManualCookieHeader,
        string.IsNullOrWhiteSpace(CursorManualCookieHeader) ? null : CursorManualCookieHeader);
}
