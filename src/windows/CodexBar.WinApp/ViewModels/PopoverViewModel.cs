using CodexBar.Core.Models;
using System.Windows.Input;

namespace CodexBar.WinApp.ViewModels;

public sealed class PopoverViewModel
{
    public PopoverViewModel(
        IReadOnlyList<UsageSnapshot> snapshots,
        UsageProvider activeProvider,
        bool showUsageAsUsed,
        Action? openDashboard = null,
        Action? openSettings = null,
        Action? showAbout = null,
        Action? quit = null)
    {
        Snapshots = snapshots;
        ShowUsageAsUsed = showUsageAsUsed;
        ActiveSnapshot = snapshots.FirstOrDefault(snapshot => snapshot.Provider == activeProvider) ?? snapshots.FirstOrDefault();
        ActiveProvider = ActiveSnapshot?.Provider ?? activeProvider;
        Tabs = snapshots.Select(snapshot => new ProviderTabViewModel(
            snapshot.Provider,
            snapshot.DisplayName,
            FormatPercent(snapshot.Windows.FirstOrDefault(), showUsageAsUsed),
            snapshot.Provider == ActiveProvider,
            snapshot.IsStale)).ToArray();
        UsageDashboardCommand = new ActionCommand(openDashboard);
        SettingsCommand = new ActionCommand(openSettings);
        AboutCommand = new ActionCommand(showAbout);
        QuitCommand = new ActionCommand(quit);
    }

    public IReadOnlyList<UsageSnapshot> Snapshots { get; }
    public UsageProvider ActiveProvider { get; }
    public bool ShowUsageAsUsed { get; }
    public UsageSnapshot? ActiveSnapshot { get; }
    public IReadOnlyList<ProviderTabViewModel> Tabs { get; }
    public ICommand UsageDashboardCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand QuitCommand { get; }

    private static string FormatPercent(RateWindow? window, bool showUsageAsUsed)
    {
        if (window is null)
        {
            return "--";
        }

        var value = showUsageAsUsed ? window.UsedPercent : window.PercentLeft;
        return $"{Math.Round(value):0}%";
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
}
