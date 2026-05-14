using System;
using System.Collections.Generic;
using System.Linq;
using CodexBar.Core.Models;
using CodexBar.Core.Refresh;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodexBar.WinUI.ViewModels;

public sealed partial class PopoverViewModel : ObservableObject
{
    private readonly bool showUsageAsUsed;
    private readonly ProviderRefreshStateRegistry? refreshStates;
    private readonly DateTimeOffset? now;

    [ObservableProperty] private UsageProvider activeProvider;
    [ObservableProperty] private UsageSnapshot? activeSnapshot;
    [ObservableProperty] private IReadOnlyList<ProviderTabViewModel> tabs = Array.Empty<ProviderTabViewModel>();
    [ObservableProperty] private IReadOnlyList<PopoverMetricViewModel> metrics = Array.Empty<PopoverMetricViewModel>();
    [ObservableProperty] private string updatedText = string.Empty;
    [ObservableProperty] private string planText = string.Empty;
    [ObservableProperty] private string liveIndicatorText = string.Empty;

    public IReadOnlyList<UsageSnapshot> Snapshots { get; }

    public PopoverViewModel(
        IReadOnlyList<UsageSnapshot> snapshots,
        UsageProvider activeProvider,
        bool showUsageAsUsed,
        ProviderRefreshStateRegistry? refreshStates = null,
        DateTimeOffset? now = null)
    {
        Snapshots = snapshots;
        this.showUsageAsUsed = showUsageAsUsed;
        this.refreshStates = refreshStates;
        this.now = now;
        SelectProvider(activeProvider);
    }

    public void SelectProvider(UsageProvider provider)
    {
        var selected = Snapshots.FirstOrDefault(s => s.Provider == provider) ?? Snapshots.FirstOrDefault();
        ActiveProvider = selected?.Provider ?? provider;
        ActiveSnapshot = selected;
        Tabs = Snapshots.Select(s => new ProviderTabViewModel(
            s.Provider,
            s.DisplayName,
            s.Provider == ActiveProvider,
            s.IsStale)).ToArray();
        Metrics = BuildMetrics(selected);
        PlanText = selected?.Plan ?? string.Empty;
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
