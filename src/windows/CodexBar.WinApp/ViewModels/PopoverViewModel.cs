using CodexBar.Core.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CodexBar.WinApp.ViewModels;

public sealed class PopoverViewModel : INotifyPropertyChanged
{
    private UsageProvider activeProvider;
    private UsageSnapshot? activeSnapshot;
    private IReadOnlyList<ProviderTabViewModel> tabs = Array.Empty<ProviderTabViewModel>();

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

    public ICommand SelectProviderCommand { get; }
    public ICommand UsageDashboardCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand QuitCommand { get; }

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
            snapshot.Provider == ActiveProvider,
            snapshot.IsStale)).ToArray();
    }

    private static string FormatPercent(RateWindow? window, bool showUsageAsUsed)
    {
        if (window is null)
        {
            return "--";
        }

        var value = showUsageAsUsed ? window.UsedPercent : window.PercentLeft;
        return $"{Math.Round(value):0}%";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
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
