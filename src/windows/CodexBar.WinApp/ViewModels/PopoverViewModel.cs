using CodexBar.Core.Models;
using CodexBar.Core.Refresh;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CodexBar.WinApp.ViewModels;

public sealed class PopoverViewModel : INotifyPropertyChanged
{
    private UsageProvider activeProvider;
    private UsageSnapshot? activeSnapshot;
    private IReadOnlyList<ProviderTabViewModel> tabs = Array.Empty<ProviderTabViewModel>();
    private IReadOnlyList<PopoverMetricViewModel> metrics = Array.Empty<PopoverMetricViewModel>();
    private string updatedText = string.Empty;
    private string planText = string.Empty;
    private string costTodayText = string.Empty;
    private string costLast30DaysText = string.Empty;
    private string statusMessage = string.Empty;
    private string liveIndicatorText = string.Empty;
    private bool hasMetrics;
    private bool hasStatusMessage;
    private bool hasCostSection;
    private readonly DateTimeOffset? now;
    private readonly ProviderRefreshStateRegistry? refreshStates;

    public PopoverViewModel(
        IReadOnlyList<UsageSnapshot> snapshots,
        UsageProvider activeProvider,
        bool showUsageAsUsed,
        Action? openDashboard = null,
        Action? openSettings = null,
        Action? showAbout = null,
        Action? quit = null,
        Action? addAccount = null,
        Action? openStatusPage = null,
        DateTimeOffset? now = null,
        ProviderRefreshStateRegistry? refreshStates = null)
    {
        Snapshots = snapshots;
        ShowUsageAsUsed = showUsageAsUsed;
        this.now = now;
        this.refreshStates = refreshStates;
        SelectProviderCommand = new ParameterCommand(parameter =>
        {
            if (parameter is UsageProvider provider)
            {
                SelectProvider(provider);
            }
        });
        UsageDashboardCommand = new ActionCommand(openDashboard);
        SettingsCommand = new ActionCommand(openSettings);
        AboutCommand = new ActionCommand(showAbout);
        QuitCommand = new ActionCommand(quit);
        AddAccountCommand = new ActionCommand(addAccount ?? openSettings);
        StatusPageCommand = new ActionCommand(openStatusPage);
        var footerRows = new[]
        {
            new PopoverFooterRowViewModel("Add Account...", "\uE72E", true, AddAccountCommand),
            new PopoverFooterRowViewModel("Usage Dashboard", "\uE9D2", true, UsageDashboardCommand),
            new PopoverFooterRowViewModel("Status Page", "\uE9D9", true, StatusPageCommand),
            new PopoverFooterRowViewModel("Settings...", "\uE713", true, SettingsCommand),
            new PopoverFooterRowViewModel("About CodexBar", "\uE946", true, AboutCommand),
            new PopoverFooterRowViewModel("Quit", "\uE8BB", true, QuitCommand)
        };
        FooterRows = footerRows;
        FooterPrimaryRows = footerRows.Take(3).ToArray();
        FooterSecondaryRows = footerRows.Skip(3).ToArray();
        BottomActionRows = FooterPrimaryRows;
        TopChromeRows = FooterSecondaryRows;
        SelectProvider(activeProvider);
    }

    public IReadOnlyList<UsageSnapshot> Snapshots { get; }
    public UsageProvider ActiveProvider
    {
        get => activeProvider;
        private set => SetField(ref activeProvider, value);
    }

    public bool ShowUsageAsUsed { get; }
    public UsageSnapshot? ActiveSnapshot
    {
        get => activeSnapshot;
        private set => SetField(ref activeSnapshot, value);
    }

    public IReadOnlyList<ProviderTabViewModel> Tabs
    {
        get => tabs;
        private set => SetField(ref tabs, value);
    }

    public IReadOnlyList<PopoverMetricViewModel> Metrics
    {
        get => metrics;
        private set
        {
            if (SetField(ref metrics, value))
            {
                HasMetrics = value.Count > 0;
            }
        }
    }

    public string UpdatedText
    {
        get => updatedText;
        private set => SetField(ref updatedText, value);
    }

    public string PlanText
    {
        get => planText;
        private set => SetField(ref planText, value);
    }

    public string CostTodayText
    {
        get => costTodayText;
        private set => SetField(ref costTodayText, value);
    }

    public string CostLast30DaysText
    {
        get => costLast30DaysText;
        private set => SetField(ref costLast30DaysText, value);
    }

    public bool HasCostSection
    {
        get => hasCostSection;
        private set => SetField(ref hasCostSection, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public bool HasStatusMessage
    {
        get => hasStatusMessage;
        private set => SetField(ref hasStatusMessage, value);
    }

    public bool HasMetrics
    {
        get => hasMetrics;
        private set => SetField(ref hasMetrics, value);
    }

    public string LiveIndicatorText
    {
        get => liveIndicatorText;
        private set => SetField(ref liveIndicatorText, value);
    }

    public ICommand SelectProviderCommand { get; }
    public ICommand AddAccountCommand { get; }
    public ICommand UsageDashboardCommand { get; }
    public ICommand StatusPageCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand QuitCommand { get; }
    public IReadOnlyList<PopoverFooterRowViewModel> FooterRows { get; }
    public IReadOnlyList<PopoverFooterRowViewModel> FooterPrimaryRows { get; }
    public IReadOnlyList<PopoverFooterRowViewModel> FooterSecondaryRows { get; }
    public IReadOnlyList<PopoverFooterRowViewModel> TopChromeRows { get; }
    public IReadOnlyList<PopoverFooterRowViewModel> BottomActionRows { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SelectProvider(UsageProvider provider)
    {
        var selectedSnapshot = Snapshots.FirstOrDefault(snapshot => snapshot.Provider == provider) ?? Snapshots.FirstOrDefault();
        ActiveSnapshot = selectedSnapshot;
        ActiveProvider = selectedSnapshot?.Provider ?? provider;
        Tabs = Snapshots.Select(snapshot => new ProviderTabViewModel(
            snapshot.Provider,
            snapshot.DisplayName,
            FormatPercent(snapshot.Windows.FirstOrDefault(), ShowUsageAsUsed),
            FormatPercentValue(snapshot.Windows.FirstOrDefault(), ShowUsageAsUsed),
            ProviderIconGeometry(snapshot.Provider),
            ProgressColor(snapshot.Provider),
            snapshot.Provider == ActiveProvider,
            snapshot.IsStale)).ToArray();
        Metrics = BuildMetrics(selectedSnapshot);
        StatusMessage = BuildStatusMessage(selectedSnapshot);
        HasStatusMessage = !string.IsNullOrWhiteSpace(StatusMessage);
        UpdatedText = selectedSnapshot is null ? string.Empty : $"Updated {FormatUpdatedText(selectedSnapshot.UpdatedAt, now ?? DateTimeOffset.Now)}";
        PlanText = selectedSnapshot?.Plan ?? string.Empty;
        CostTodayText = FormatCostLine("Today", selectedSnapshot?.TodayCostUsd, selectedSnapshot?.TodayTokens);
        CostLast30DaysText = FormatCostLine("Last 30 days", selectedSnapshot?.Last30DaysCostUsd, selectedSnapshot?.Last30DaysTokens);
        HasCostSection = selectedSnapshot?.TodayCostUsd is not null ||
            selectedSnapshot?.TodayTokens is not null ||
            selectedSnapshot?.Last30DaysCostUsd is not null ||
            selectedSnapshot?.Last30DaysTokens is not null;
        UpdateLiveIndicator();
    }

    public void RefreshLiveIndicator()
    {
        UpdateLiveIndicator();
        RefreshUpdatedText();
    }

    private void RefreshUpdatedText()
    {
        if (activeSnapshot is null)
        {
            UpdatedText = string.Empty;
            return;
        }
        UpdatedText = $"Updated {FormatUpdatedText(activeSnapshot.UpdatedAt, now ?? DateTimeOffset.Now)}";
    }

    private void UpdateLiveIndicator()
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
        var currentTime = now ?? DateTimeOffset.Now;
        var diff = currentTime - last.Value;
        LiveIndicatorText = $"Live • updated {HumanizeDiff(diff)} ago";
    }

    private static string HumanizeDiff(TimeSpan diff) => diff switch
    {
        { TotalSeconds: < 60 } => $"{Math.Max(0, (int)diff.TotalSeconds)}s",
        { TotalMinutes: < 60 } => $"{(int)diff.TotalMinutes}m",
        _ => $"{(int)diff.TotalHours}h",
    };

    private static string FormatPercent(RateWindow? window, bool showUsageAsUsed)
    {
        if (window is null)
        {
            return "--";
        }

        var value = showUsageAsUsed ? window.UsedPercent : window.PercentLeft;
        return $"{Math.Round(value):0}%";
    }

    private IReadOnlyList<PopoverMetricViewModel> BuildMetrics(UsageSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return Array.Empty<PopoverMetricViewModel>();
        }

        if (snapshot.Windows.Count == 0)
        {
            return Array.Empty<PopoverMetricViewModel>();
        }

        var currentTime = now ?? DateTimeOffset.Now;
        return snapshot.Windows.Select(window => new PopoverMetricViewModel(
            window.Title,
            FormatPercentValue(window, ShowUsageAsUsed),
            $"{FormatPercent(window, ShowUsageAsUsed)} {(ShowUsageAsUsed ? "used" : "left")}",
            window.ResetsAt is null ? string.Empty : $"Resets {FormatRelative(window.ResetsAt.Value, currentTime)}",
            ProgressColor(snapshot.Provider))).ToArray();
    }

    private static string BuildStatusMessage(UsageSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
        {
            return snapshot.ErrorMessage;
        }

        return snapshot.Windows.Count == 0
            ? "No usage windows are available for this provider."
            : string.Empty;
    }

    private static double FormatPercentValue(RateWindow? window, bool showUsageAsUsed)
    {
        if (window is null)
        {
            return 0;
        }

        return Math.Clamp(showUsageAsUsed ? window.UsedPercent : window.PercentLeft, 0, 100);
    }

    private static string FormatUpdatedText(DateTimeOffset updatedAt, DateTimeOffset now)
    {
        var delta = now - updatedAt;
        if (delta.TotalMinutes < 1)
        {
            return "just now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)Math.Floor(delta.TotalMinutes))}m ago";
        }

        if (delta.TotalDays < 1)
        {
            return $"{(int)Math.Floor(delta.TotalHours)}h ago";
        }

        return updatedAt.ToString("t", CultureInfo.CurrentCulture);
    }

    private static string FormatRelative(DateTimeOffset target, DateTimeOffset now)
    {
        var delta = target - now;
        if (delta.TotalMinutes < 1)
        {
            return "now";
        }

        if (delta.TotalHours < 1)
        {
            return $"in {Math.Max(1, (int)Math.Floor(delta.TotalMinutes))}m";
        }

        if (delta.TotalDays < 1)
        {
            var hours = (int)Math.Floor(delta.TotalHours);
            var minutes = (int)Math.Floor(delta.TotalMinutes - (hours * 60));
            return minutes > 0 ? $"in {hours}h {minutes}m" : $"in {hours}h";
        }

        var days = (int)Math.Floor(delta.TotalDays);
        var remainingHours = (int)Math.Floor(delta.TotalHours - (days * 24));
        return remainingHours > 0 ? $"in {days}d {remainingHours}h" : $"in {days}d";
    }

    private static string FormatCostLine(string label, decimal? costUsd, long? tokens)
    {
        var cost = costUsd?.ToString("$0.00", CultureInfo.InvariantCulture) ?? "--";
        if (tokens is null)
        {
            return $"{label}: {cost}";
        }

        return $"{label}: {cost} \u00b7 {FormatTokens(tokens.Value)} tokens";
    }

    private static string FormatTokens(long tokens)
    {
        if (tokens >= 1_000_000)
        {
            return $"{Math.Round(tokens / 1_000_000d):0}M";
        }

        if (tokens >= 1_000)
        {
            return $"{Math.Round(tokens / 1_000d):0}K";
        }

        return tokens.ToString(CultureInfo.InvariantCulture);
    }

    private static string ProgressColor(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => "#C87950",
            UsageProvider.Cursor => "#2F7BF6",
            UsageProvider.Gemini => "#5FAD56",
            _ => "#56B3A7"
        };

    private static string ProviderIconGeometry(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Claude => ClaudeIconGeometry,
            // Preview providers reuse the neutral Codex vector until provider-specific icons are added.
            UsageProvider.Cursor => CodexIconGeometry,
            UsageProvider.Gemini => CodexIconGeometry,
            _ => CodexIconGeometry
        };

    private const string CodexIconGeometry = "M83.7733 42.8087C84.6678 40.1149 84.9771 37.2613 84.6807 34.4385C84.3843 31.6156 83.489 28.8885 82.0544 26.4394C77.6908 18.8436 68.9203 14.9365 60.3548 16.7725C57.9831 14.1344 54.9591 12.1668 51.5864 11.0673C48.2137 9.96772 44.611 9.77498 41.1402 10.5084C37.6694 11.2418 34.4527 12.8755 31.8132 15.2455C29.1736 17.6155 27.204 20.6383 26.1024 24.0103C23.3212 24.5806 20.6938 25.738 18.3958 27.405C16.0977 29.0721 14.1819 31.2104 12.7765 33.6772C8.36538 41.2609 9.3669 50.8267 15.2527 57.3327C14.3549 60.0251 14.0424 62.8782 14.3361 65.7012C14.6298 68.5241 15.523 71.2518 16.9558 73.7017C21.325 81.3002 30.1011 85.207 38.6712 83.3686C40.5554 85.4904 42.8707 87.1858 45.4623 88.3416C48.0539 89.4975 50.8622 90.0871 53.6999 90.0713C62.4793 90.079 70.2575 84.4114 72.9393 76.0515C75.7201 75.4802 78.347 74.3225 80.6449 72.6555C82.9427 70.9886 84.8587 68.8507 86.2649 66.3846C90.6227 58.8145 89.6172 49.3005 83.7733 42.8087ZM53.6999 84.8356C50.1955 84.8411 46.801 83.6129 44.1116 81.3661L44.5848 81.098L60.5123 71.9043C60.9087 71.6718 61.2379 71.3402 61.4674 70.942C61.6969 70.5439 61.8189 70.0929 61.8215 69.6333V47.1769L68.5553 51.072C68.6225 51.1063 68.6694 51.1707 68.6814 51.2456V69.854C68.6641 78.1208 61.9667 84.8183 53.6999 84.8356ZM21.4977 71.0843C19.7402 68.0497 19.1092 64.4925 19.7156 61.0386L20.1885 61.3225L36.1321 70.5165C36.5266 70.748 36.9757 70.87 37.4331 70.87C37.8905 70.87 38.3396 70.748 38.7341 70.5165L58.21 59.2883V67.0628C58.2081 67.1031 58.1973 67.1424 58.1782 67.1779C58.1591 67.2134 58.1322 67.2441 58.0996 67.2678L41.9671 76.5722C34.798 80.7022 25.6388 78.2463 21.4977 71.0843ZM17.3026 36.3898C19.0723 33.3357 21.8655 31.0062 25.1878 29.8138V48.7376C25.1818 49.1949 25.2986 49.6453 25.5261 50.042C25.7535 50.4387 26.0833 50.7671 26.4809 50.9928L45.8622 62.1739L39.1283 66.069C39.0919 66.0883 39.0513 66.0984 39.0101 66.0984C38.9689 66.0984 38.9283 66.0883 38.8919 66.069L22.7908 56.7809C15.6359 52.6337 13.1822 43.4816 17.3026 36.3112V36.3898ZM72.624 49.2426L53.1792 37.9512L59.8976 34.0718C59.9341 34.0524 59.9747 34.0423 60.016 34.0423C60.0573 34.0423 60.0979 34.0524 60.1344 34.0718L76.2355 43.3761C78.6973 44.7966 80.7043 46.8882 82.0221 49.4065C83.3398 51.9249 83.914 54.7661 83.6775 57.5985C83.4411 60.431 82.4038 63.1377 80.6867 65.4027C78.9696 67.6677 76.6436 69.3975 73.9803 70.3901V51.466C73.9663 51.0096 73.834 50.5647 73.5962 50.1749C73.3584 49.7851 73.0234 49.4638 72.624 49.2426ZM79.3261 39.1657L78.8529 38.8815L62.9411 29.6089C62.5442 29.376 62.0924 29.2532 61.6322 29.2532C61.172 29.2532 60.7202 29.376 60.3233 29.6089L40.8629 40.8374V33.0628C40.8587 33.0233 40.8654 32.9834 40.882 32.9473C40.8987 32.9113 40.9248 32.8803 40.9575 32.8579L57.0586 23.5692C59.5263 22.1476 62.3478 21.458 65.193 21.5811C68.0382 21.7042 70.7896 22.6348 73.1253 24.2642C75.461 25.8936 77.2845 28.1543 78.3825 30.782C79.4806 33.4097 79.8077 36.2957 79.3257 39.1025V39.1657H79.3261ZM37.1888 52.9484L30.455 49.069C30.4213 49.0487 30.3925 49.0212 30.3707 48.9884C30.3488 48.9557 30.3345 48.9186 30.3286 48.8797V30.3188C30.3323 27.4714 31.1466 24.6839 32.6761 22.2822C34.2057 19.8805 36.3874 17.9639 38.9661 16.7564C41.5448 15.549 44.4139 15.1005 47.2381 15.4636C50.0622 15.8267 52.7247 16.9862 54.9141 18.8067L54.4409 19.0748L38.5134 28.2686C38.117 28.5011 37.7879 28.8327 37.5584 29.2308C37.329 29.629 37.207 30.0799 37.2045 30.5395L37.1888 52.9487V52.9484ZM40.8472 45.0632L49.5209 40.0643L58.21 45.0635V55.0615L49.5523 60.0608L40.8632 55.0615L40.8472 45.0632Z";

    private const string ClaudeIconGeometry = "M25.7146 63.2153L41.4393 54.3917L41.7025 53.6226L41.4393 53.1976H40.6705L38.0394 53.0359L29.054 52.7929L21.2624 52.4691L13.7134 52.0644L11.8111 51.6594L10.0303 49.3118L10.2123 48.138L11.8111 47.0657L14.0981 47.2681L19.1574 47.6119L26.7467 48.138L32.2516 48.4618L40.4073 49.3118H41.7025L41.8846 48.7857L41.4393 48.4618L41.0955 48.138L33.243 42.8155L24.7432 37.1894L20.2909 33.9513L17.8824 32.3119L16.6684 30.774L16.1422 27.4147L18.328 25.0062L21.2624 25.2088L22.0112 25.4112L24.9861 27.6979L31.3407 32.616L39.6381 38.7273L40.8525 39.7391L41.3381 39.395L41.399 39.1523L40.8525 38.2415L36.3394 30.0858L31.5227 21.7883L29.3775 18.3478L28.811 16.2837C28.6087 15.4334 28.4669 14.7252 28.4669 13.8549L30.9563 10.4753L32.3321 10.0303L35.6515 10.4756L37.0479 11.6897L39.112 16.4052L42.4513 23.8327L47.6321 33.9313L49.15 36.9265L49.9594 39.6991L50.2632 40.5491H50.7894V40.0632L51.2141 34.3766L52.0035 27.3944L52.7726 18.4087L53.0358 15.8793L54.2905 12.8435L56.7795 11.2041L58.7224 12.135L60.3212 14.422L60.0986 15.899L59.1474 22.0718L57.2857 31.7458L56.0713 38.2218H56.7795L57.5892 37.4121L60.8677 33.061L66.3723 26.18L68.801 23.448L71.6342 20.4325L73.4556 18.9957H76.8962L79.4255 22.7601L78.2926 26.6456L74.7509 31.1384L71.8163 34.943L67.607 40.6097L64.9758 45.1431L65.2188 45.5072L65.8464 45.4466L75.358 43.4228L80.4984 42.4917L86.6304 41.4393L89.4033 42.7346L89.7065 44.0502L88.6135 46.7419L82.0566 48.3607L74.3662 49.8989L62.9118 52.6109L62.77 52.7121L62.9321 52.9144L68.0925 53.4L70.2987 53.5214H75.7021L85.7601 54.2702L88.3912 56.0108L89.9697 58.1358L89.7065 59.7545L85.6589 61.8189L80.1949 60.5236L67.4452 57.4881L63.0735 56.3952H62.4665V56.7596L66.1093 60.3213L72.7877 66.3523L81.1461 74.1236L81.5707 76.0462L80.4984 77.5638L79.3649 77.4021L72.0186 71.8772L69.1854 69.3879L62.77 63.9844H62.3453V64.5509L63.8223 66.7164L71.6342 78.4544L72.0389 82.0567L71.4725 83.2308L69.4487 83.939L67.2222 83.534L62.6485 77.1189L57.9333 69.8937L54.1284 63.4177L53.6631 63.6809L51.4167 87.8651L50.3644 89.0995L47.9356 90.0303L45.9121 88.4924L44.8392 86.0031L45.9118 81.0852L47.2071 74.6701L48.2594 69.5699L49.2106 63.2356L49.7773 61.131L49.7367 60.9892L49.2715 61.0498L44.4954 67.607L37.23 77.4224L31.4825 83.5746L30.1063 84.1211L27.7181 82.8864L27.9408 80.6805L29.2763 78.7177L37.2297 68.5988L42.026 62.3248L45.1227 58.7025L45.1024 58.176H44.9204L23.7917 71.8975L20.0274 72.3831L18.4083 70.8655L18.6106 68.3761L19.3798 67.5664L25.7343 63.195L25.7146 63.2153Z";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private sealed class ActionCommand : ICommand
    {
        private readonly Action action;

        public ActionCommand(Action? action)
        {
            this.action = action ?? NoOp;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            action();
        }

        private static void NoOp()
        {
        }
    }

    private sealed class ParameterCommand : ICommand
    {
        private readonly Action<object?> action;

        public ParameterCommand(Action<object?> action)
        {
            this.action = action;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            action(parameter);
        }
    }
}
