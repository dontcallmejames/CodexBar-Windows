using CodexBar.Core;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Core.Settings;
using CodexBar.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodexBar.WinUI.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    // Original settings kept so unchanged fields survive the round-trip.
    private readonly AppSettings originalSettings;
    private readonly Func<IReadOnlyList<UsageSnapshot>> snapshotsProvider;
    private readonly Func<UpdateCheckResult?> lastUpdateProvider;

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
        : this(settings, static () => Array.Empty<UsageSnapshot>(), static () => null)
    {
    }

    public SettingsViewModel(
        AppSettings settings,
        Func<IReadOnlyList<UsageSnapshot>> snapshotsProvider,
        Func<UpdateCheckResult?> lastUpdateProvider)
    {
        originalSettings = settings;
        this.snapshotsProvider = snapshotsProvider;
        this.lastUpdateProvider = lastUpdateProvider;
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

    [RelayCommand]
    private void OpenBugReport()
    {
        try
        {
            var summary = BugReportBuilder.BuildDiagnosticSummary(
                ToSettings(),
                snapshotsProvider(),
                updateStatus: lastUpdateProvider());

            var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
            pkg.SetText(summary);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);

            var uri = ProviderLinks.BugReportUri();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // Swallow — failing to copy/open shouldn't crash settings.
        }
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
