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

    [ObservableProperty] private UsageProvider activeProvider;
    [ObservableProperty] private UsageSnapshot? activeSnapshot;
    [ObservableProperty] private IReadOnlyList<PopoverMetricViewModel> metrics = Array.Empty<PopoverMetricViewModel>();
    [ObservableProperty] private string updatedText = string.Empty;
    [ObservableProperty] private string planText = string.Empty;
    [ObservableProperty] private string liveIndicatorText = string.Empty;
    [ObservableProperty] private string localTokensText = string.Empty;
    [ObservableProperty] private Microsoft.UI.Xaml.Visibility localTokensVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;

    public IReadOnlyList<UsageSnapshot> Snapshots { get; private set; }

    public PopoverViewModel(
        IReadOnlyList<UsageSnapshot> snapshots,
        UsageProvider activeProvider,
        bool showUsageAsUsed,
        ProviderRefreshStateRegistry? refreshStates = null,
        DateTimeOffset? now = null,
        Action? openSettings = null,
        Action? openAbout = null,
        Action? quit = null,
        Action? openDashboard = null,
        Action? openStatusPage = null,
        Action? openAddAccount = null)
    {
        Snapshots = snapshots;
        this.showUsageAsUsed = showUsageAsUsed;
        this.refreshStates = refreshStates;
        this.now = now;
        this.openSettings = openSettings;
        this.openAbout = openAbout;
        this.quit = quit;
        this.openDashboard = openDashboard;
        this.openStatusPage = openStatusPage;
        this.openAddAccount = openAddAccount;
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

    public void SelectProvider(UsageProvider provider)
    {
        var selected = Snapshots.FirstOrDefault(s => s.Provider == provider) ?? Snapshots.FirstOrDefault();
        ActiveProvider = selected?.Provider ?? provider;
        ActiveSnapshot = selected;
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
