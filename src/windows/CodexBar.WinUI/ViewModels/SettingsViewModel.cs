using System.IO;
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
    private readonly IUpdateInstaller? updateInstaller;
    private readonly Func<string, (bool Success, string? ErrorMessage)>? launchInstaller;
    private readonly Action? quitApp;
    private readonly Func<Task>? checkForUpdates;

    [ObservableProperty] private bool codexEnabled;
    [ObservableProperty] private bool claudeEnabled;
    [ObservableProperty] private bool cursorEnabled;
    [ObservableProperty] private bool geminiEnabled;
    [ObservableProperty] private bool copilotEnabled;
    // NumberBox.Value is double — back this with a double so the binding doesn't quietly fail.
    [ObservableProperty] private double refreshMinutes;
    [ObservableProperty] private bool dockOverviewNearTaskbar;
    [ObservableProperty] private bool launchAtStartup;
    [ObservableProperty] private bool checkForUpdatesAutomatically;
    [ObservableProperty] private bool showUsageAsUsed;
    [ObservableProperty] private string claudeManualCookieHeader = string.Empty;
    [ObservableProperty] private string cursorManualCookieHeader = string.Empty;
    [ObservableProperty] private string globalHotkey = "Ctrl+Alt+U";
    [ObservableProperty] private bool enableGlobalHotkey = true;

    // In-app update installer state.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallUpdate))]
    [NotifyPropertyChangedFor(nameof(ProgressVisibility))]
    private bool isInstalling;
    [ObservableProperty] private double downloadProgress;
    [ObservableProperty] private string installStatusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    private bool isCheckingForUpdates;

    public Microsoft.UI.Xaml.Visibility ProgressVisibility =>
        IsInstalling ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public SettingsViewModel(AppSettings settings)
        : this(settings, static () => Array.Empty<UsageSnapshot>(), static () => null)
    {
    }

    public SettingsViewModel(
        AppSettings settings,
        Func<IReadOnlyList<UsageSnapshot>> snapshotsProvider,
        Func<UpdateCheckResult?> lastUpdateProvider)
        : this(settings, snapshotsProvider, lastUpdateProvider, null, null, null)
    {
    }

    public SettingsViewModel(
        AppSettings settings,
        Func<IReadOnlyList<UsageSnapshot>> snapshotsProvider,
        Func<UpdateCheckResult?> lastUpdateProvider,
        IUpdateInstaller? updateInstaller,
        Func<string, (bool Success, string? ErrorMessage)>? launchInstaller,
        Action? quitApp,
        Func<Task>? checkForUpdates = null)
    {
        originalSettings = settings;
        this.snapshotsProvider = snapshotsProvider;
        this.lastUpdateProvider = lastUpdateProvider;
        this.updateInstaller = updateInstaller;
        this.launchInstaller = launchInstaller;
        this.quitApp = quitApp;
        this.checkForUpdates = checkForUpdates;
        codexEnabled = settings.CodexEnabled;
        claudeEnabled = settings.ClaudeEnabled;
        cursorEnabled = settings.CursorEnabled;
        geminiEnabled = settings.GeminiEnabled;
        copilotEnabled = settings.CopilotEnabled;
        refreshMinutes = settings.RefreshMinutes;
        dockOverviewNearTaskbar = settings.DockOverviewNearTaskbar;
        launchAtStartup = settings.LaunchAtStartup;
        checkForUpdatesAutomatically = settings.CheckForUpdatesAutomatically;
        showUsageAsUsed = settings.ShowUsageAsUsed;
        claudeManualCookieHeader = settings.ClaudeManualCookieHeader ?? string.Empty;
        cursorManualCookieHeader = settings.CursorManualCookieHeader ?? string.Empty;
        globalHotkey = string.IsNullOrWhiteSpace(settings.GlobalHotkey) ? "Ctrl+Alt+U" : settings.GlobalHotkey;
        enableGlobalHotkey = settings.EnableGlobalHotkey;
        UpdateAvailableStatus();
    }

    /// <summary>
    /// Latest tag string shown in the update card. Empty when no update info has loaded.
    /// </summary>
    public string LatestUpdateText
    {
        get
        {
            var result = lastUpdateProvider();
            if (result is null) return "Checking for updates…";
            if (!string.IsNullOrEmpty(result.ErrorMessage)) return result.StatusText;
            if (result.UpdateAvailable && result.LatestTag is not null) return $"{result.LatestTag} available.";
            return result.StatusText;
        }
    }

    /// <summary>
    /// True when an update is available, both installer + sidecar URLs were discovered in the
    /// release assets, and no install is currently in flight.
    /// </summary>
    public bool CanInstallUpdate
    {
        get
        {
            if (IsInstalling) return false;
            if (updateInstaller is null || launchInstaller is null) return false;
            var result = lastUpdateProvider();
            return result is { UpdateAvailable: true, InstallerAssetUri: not null, InstallerSha256Uri: not null };
        }
    }

    /// <summary>
    /// Re-reads the latest update result and refreshes derived bindings. Call from the host
    /// when UpdateNotifier.ResultChanged fires.
    /// </summary>
    public void UpdateAvailableStatus()
    {
        OnPropertyChanged(nameof(CanInstallUpdate));
        OnPropertyChanged(nameof(LatestUpdateText));
        InstallUpdateCommand.NotifyCanExecuteChanged();
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
            CodexBar.WinUI.Services.ExternalLauncher.OpenExternalUrl(uri);
        }
        catch
        {
            // Swallow — failing to copy/open shouldn't crash settings.
        }
    }

    /// <summary>
    /// Can run a manual update check whenever the host supplied a checker and one isn't already running.
    /// </summary>
    public bool CanCheckNow => !IsCheckingForUpdates && checkForUpdates is not null;

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private async Task CheckNow()
    {
        if (checkForUpdates is null) return;
        IsCheckingForUpdates = true;
        InstallStatusText = "Checking for updates…";
        try
        {
            await checkForUpdates();
        }
        catch (Exception ex)
        {
            InstallStatusText = $"Update check failed: {ex.Message}";
            IsCheckingForUpdates = false;
            return;
        }

        IsCheckingForUpdates = false;
        // The notifier's ResultChanged also refreshes these via the host; do it here too so
        // the status reflects the result immediately even if that event is missed.
        UpdateAvailableStatus();
        InstallStatusText = LatestUpdateText;
    }

    [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
    private async Task InstallUpdate()
    {
        if (updateInstaller is null || launchInstaller is null) return;
        var result = lastUpdateProvider();
        if (result is null || result.InstallerAssetUri is null || result.InstallerSha256Uri is null) return;

        IsInstalling = true;
        DownloadProgress = 0;
        InstallStatusText = "Downloading…";

        // Sweep prior orphans from %TEMP% — every failed UAC prompt leaves a ~50MB
        // installer behind. Best-effort: never throw out of a cleanup pass.
        TryCleanupOrphanInstallers();

        try
        {
            var progress = new Progress<double>(value =>
            {
                DownloadProgress = value * 100.0;
                InstallStatusText = value >= 1.0
                    ? "Verifying signature…"
                    : $"Downloading {Math.Round(value * 100.0)}%";
            });

            var prepared = await updateInstaller.PrepareAsync(
                result.InstallerAssetUri,
                result.InstallerSha256Uri,
                progress,
                CancellationToken.None);

            if (!prepared.Success || prepared.LocalInstallerPath is null)
            {
                InstallStatusText = $"Install failed: {prepared.ErrorMessage ?? "unknown error"}";
                IsInstalling = false;
                return;
            }

            InstallStatusText = "Launching installer…";
            var (launched, errorMessage) = launchInstaller(prepared.LocalInstallerPath);
            if (!launched)
            {
                // UAC denial (ERROR_CANCELLED) is the common path here. The downloaded
                // ~50MB installer would otherwise linger in %TEMP% across retries.
                try { File.Delete(prepared.LocalInstallerPath); } catch { /* best effort */ }
                InstallStatusText = $"Install failed: {errorMessage ?? "could not launch installer"}";
                IsInstalling = false;
                return;
            }

            // Hand off to the installer process and shut down so it can replace our binaries.
            quitApp?.Invoke();
        }
        catch (Exception ex)
        {
            InstallStatusText = $"Install failed: {ex.Message}";
            IsInstalling = false;
        }
    }

    public AppSettings ToSettings() => new(
        CodexEnabled,
        ClaudeEnabled,
        CursorEnabled,
        GeminiEnabled,
        CopilotEnabled,
        originalSettings.MergeTrayIcon,
        ShowUsageAsUsed,
        DockOverviewNearTaskbar,
        LaunchAtStartup,
        CheckForUpdatesAutomatically,
        ClampRefreshMinutes(),
        originalSettings.CodexSource,
        originalSettings.ClaudeSource,
        originalSettings.CursorSource,
        originalSettings.GeminiSource,
        originalSettings.CopilotSource,
        string.IsNullOrWhiteSpace(ClaudeManualCookieHeader) ? null : ClaudeManualCookieHeader,
        string.IsNullOrWhiteSpace(CursorManualCookieHeader) ? null : CursorManualCookieHeader,
        string.IsNullOrWhiteSpace(GlobalHotkey) ? "Ctrl+Alt+U" : GlobalHotkey,
        EnableGlobalHotkey);

    private int ClampRefreshMinutes() =>
        (int)Math.Round(Math.Clamp(RefreshMinutes, 1, 1440));

    /// <summary>
    /// Remove any leftover CodexBar update installers from %TEMP%. UpdateInstaller uses
    /// the pattern "CodexBar-Windows-update-{guid}.installer.exe", so we match that
    /// exact prefix/suffix combo. Called both before download (sweep priors) and after
    /// a launch failure (delete the one we just made).
    /// </summary>
    private static void TryCleanupOrphanInstallers()
    {
        try
        {
            var tempDir = Path.GetTempPath();
            foreach (var path in Directory.EnumerateFiles(tempDir, "CodexBar-Windows-update-*.installer.exe"))
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
        }
        catch
        {
            // Enumeration can throw on unusual %TEMP% configs; never propagate.
        }
    }
}
