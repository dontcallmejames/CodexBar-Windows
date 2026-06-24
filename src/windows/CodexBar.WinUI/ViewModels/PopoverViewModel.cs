using System;
using System.Collections.Generic;
using System.Linq;
using CodexBar.Core.Models;
using CodexBar.Core.Providers.Claude;
using CodexBar.Core.Refresh;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodexBar.WinUI.ViewModels;

public sealed partial class PopoverViewModel : ObservableObject
{
    private bool showUsageAsUsed;
    private readonly ProviderRefreshStateRegistry? refreshStates;
    private readonly DateTimeOffset? now;
    private readonly Action? openSettings;
    private readonly Action? openAbout;
    private readonly Action? quit;
    private readonly Action? openDashboard;
    private readonly Action? openStatusPage;
    private readonly Action? openAddAccount;
    private readonly Action<UsageProvider>? openReconnect;

    [ObservableProperty] private UsageProvider activeProvider;
    [ObservableProperty] private UsageSnapshot? activeSnapshot;
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private string errorText = string.Empty;
    [ObservableProperty] private IReadOnlyList<PopoverMetricViewModel> metrics = Array.Empty<PopoverMetricViewModel>();
    [ObservableProperty] private string updatedText = string.Empty;
    [ObservableProperty] private string planText = string.Empty;
    [ObservableProperty] private string liveIndicatorText = string.Empty;
    [ObservableProperty] private string localTokensText = string.Empty;
    [ObservableProperty] private Microsoft.UI.Xaml.Visibility localTokensVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

    public IReadOnlyList<UsageSnapshot> Snapshots { get; private set; }

    /// <summary>
    /// Providers the user has enabled in Settings, in display order. Drives which tabs render
    /// in the popover — even if a provider has not yet produced a snapshot (no successful
    /// refresh, missing credentials), its tab still appears so the user can see "no data yet".
    /// </summary>
    public IReadOnlyList<UsageProvider> EnabledProviders { get; private set; }

    public PopoverViewModel(
        IReadOnlyList<UsageSnapshot> snapshots,
        UsageProvider activeProvider,
        bool showUsageAsUsed,
        IReadOnlyList<UsageProvider>? enabledProviders = null,
        ProviderRefreshStateRegistry? refreshStates = null,
        DateTimeOffset? now = null,
        Action? openSettings = null,
        Action? openAbout = null,
        Action? quit = null,
        Action? openDashboard = null,
        Action? openStatusPage = null,
        Action? openAddAccount = null,
        Action<UsageProvider>? openReconnect = null)
    {
        Snapshots = snapshots;
        EnabledProviders = enabledProviders ?? snapshots.Select(s => s.Provider).ToArray();
        this.showUsageAsUsed = showUsageAsUsed;
        this.refreshStates = refreshStates;
        this.now = now;
        this.openSettings = openSettings;
        this.openAbout = openAbout;
        this.quit = quit;
        this.openDashboard = openDashboard;
        this.openStatusPage = openStatusPage;
        this.openAddAccount = openAddAccount;
        this.openReconnect = openReconnect;
        SelectProvider(activeProvider);
    }

    [RelayCommand]
    private void Settings() => openSettings?.Invoke();

    [RelayCommand]
    private void About() => openAbout?.Invoke();

    [RelayCommand]
    private void Quit() => quit?.Invoke();

    [RelayCommand]
    private void Dashboard() => openDashboard?.Invoke();

    [RelayCommand]
    private void StatusPage() => openStatusPage?.Invoke();

    [RelayCommand]
    private void AddAccount() => openAddAccount?.Invoke();

    [RelayCommand]
    private void Reconnect() => openReconnect?.Invoke(ActiveProvider);

    /// <summary>
    /// Replace the snapshot set in-place and re-select the active provider so the bound
    /// XAML (which is x:Bind'd to THIS VM instance) refreshes without needing the VM
    /// to be swapped out from under the bindings.
    /// </summary>
    public void UpdateSnapshots(IReadOnlyList<UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        Snapshots = snapshots;
        this.showUsageAsUsed = showUsageAsUsed;
        // Preserve the user's current tab selection if that provider is still present;
        // otherwise SelectProvider will fall through to the first snapshot.
        SelectProvider(ActiveProvider);
    }

    /// <summary>
    /// Update the set of enabled providers (called when settings change so the popover
    /// adds or removes tabs without needing the popover to be recreated).
    /// </summary>
    public void UpdateEnabledProviders(IReadOnlyList<UsageProvider> enabledProviders)
    {
        EnabledProviders = enabledProviders;
    }

    public void SelectProvider(UsageProvider provider)
    {
        // Distinguish two no-snapshot cases:
        // (a) Provider IS in the enabled list but its snapshot is missing (newly turned on,
        //     no credentials, mid-refresh) — keep the requested provider active so the
        //     correct tab visually activates and the UI renders an empty state.
        // (b) Provider is NOT in the enabled list anymore (user just disabled it while
        //     the popover was alive) — fall back to the first enabled provider so we don't
        //     leave ActiveProvider stuck on a tab that no longer exists. Footer commands
        //     (dashboard, status) would otherwise still target the disabled provider.
        if (EnabledProviders.Count > 0 && !EnabledProviders.Contains(provider))
        {
            provider = EnabledProviders[0];
        }

        var selected = Snapshots.FirstOrDefault(s => s.Provider == provider);
        ActiveProvider = provider;
        ActiveSnapshot = selected;
        HasError = selected is { AuthState: AuthState.RequiresAuthentication };
        ErrorText = selected?.ErrorMessage ?? string.Empty;
        Metrics = BuildMetrics(selected);
        PlanText = selected?.Plan ?? string.Empty;
        UpdateLocalTokens(selected);
        RefreshLiveIndicator();
    }

    public void RefreshLiveIndicator()
    {
        RecomputeUpdatedText();
        RecomputeLiveIndicator();
    }

    private void RecomputeUpdatedText()
    {
        if (ActiveSnapshot is null)
        {
            UpdatedText = string.Empty;
            return;
        }
        var diff = (now ?? DateTimeOffset.Now) - ActiveSnapshot.UpdatedAt;
        UpdatedText = $"Updated {Humanize(diff)}";
    }

    private void RecomputeLiveIndicator()
    {
        if (refreshStates is null)
        {
            LiveIndicatorText = string.Empty;
            return;
        }
        var last = refreshStates.Get(ActiveProvider).LastSuccess;
        if (last is null)
        {
            LiveIndicatorText = "Live • Refreshing…";
            return;
        }
        var diff = (now ?? DateTimeOffset.Now) - last.Value;
        LiveIndicatorText = $"Live • updated {Humanize(diff)} ago";
    }

    private void UpdateLocalTokens(UsageSnapshot? snapshot)
    {
        if (snapshot is { Provider: UsageProvider.Claude, TodayTokens: > 0 } claudeSnapshot)
        {
            LocalTokensText = $"Claude Code: {TokenFormatter.Format(claudeSnapshot.TodayTokens.Value)} tokens today";
            LocalTokensVisibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else
        {
            LocalTokensText = string.Empty;
            LocalTokensVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        }
    }

    private IReadOnlyList<PopoverMetricViewModel> BuildMetrics(UsageSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Windows.Count == 0)
            return Array.Empty<PopoverMetricViewModel>();

        var currentTime = now ?? DateTimeOffset.Now;
        return snapshot.Windows.Select(window => new PopoverMetricViewModel(
            window.Title,
            showUsageAsUsed ? window.UsedPercent : window.PercentLeft,
            FormatPercent(window, showUsageAsUsed),
            window.ResetsAt is null ? string.Empty : $"Resets {FormatRelative(window.ResetsAt.Value, currentTime)}")).ToArray();
    }

    private static string FormatPercent(RateWindow window, bool showUsageAsUsed)
    {
        var value = showUsageAsUsed ? window.UsedPercent : window.PercentLeft;
        return $"{(int)Math.Round(value)}% {(showUsageAsUsed ? "used" : "left")}";
    }

    private static string FormatRelative(DateTimeOffset resetsAt, DateTimeOffset currentTime)
    {
        var diff = resetsAt - currentTime;
        if (diff <= TimeSpan.Zero) return "now";
        return $"in {Humanize(diff)}";
    }

    private static string Humanize(TimeSpan diff)
    {
        var abs = diff.Duration();
        return abs switch
        {
            { TotalSeconds: < 5 } => "just now",
            { TotalSeconds: < 60 } => $"{(int)abs.TotalSeconds}s",
            { TotalMinutes: < 60 } => $"{(int)abs.TotalMinutes}m",
            { TotalHours: < 24 } => $"{(int)abs.TotalHours}h",
            _ => $"{(int)abs.TotalDays}d",
        };
    }
}
