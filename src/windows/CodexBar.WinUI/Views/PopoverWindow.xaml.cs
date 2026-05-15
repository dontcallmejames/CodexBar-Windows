using System;
using CodexBar.Core.Models;
using CodexBar.WinUI.Services;
using CodexBar.WinUI.ViewModels;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT;

namespace CodexBar.WinUI.Views;

public sealed partial class PopoverWindow : Window
{
    private DesktopAcrylicController? acrylicController;
    private MicaController? micaController;
    private SystemBackdropConfiguration? backdropConfig;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? indicatorTimer;

    public PopoverViewModel ViewModel { get; private set; }

    /// <summary>
    /// Rebuilds the ViewModel from current snapshots, preserving the command callbacks
    /// captured at construction time. Called when the popover is re-shown after Hide().
    /// </summary>
    public void RefreshFromStore(
        System.Collections.Generic.IReadOnlyList<UsageSnapshot> snapshots,
        bool showUsageAsUsed,
        CodexBar.Core.Refresh.ProviderRefreshStateRegistry refreshStates)
    {
        // Build a new VM that mirrors the old one's callbacks, but with fresh snapshots.
        ViewModel = new PopoverViewModel(
            snapshots,
            ViewModel.ActiveProvider,
            showUsageAsUsed,
            refreshStates: refreshStates,
            openSettings: ViewModel.SettingsCommand.CanExecute(null) ? () => ViewModel.SettingsCommand.Execute(null) : null,
            openAbout: ViewModel.AboutCommand.CanExecute(null) ? () => ViewModel.AboutCommand.Execute(null) : null,
            quit: ViewModel.QuitCommand.CanExecute(null) ? () => ViewModel.QuitCommand.Execute(null) : null,
            openDashboard: ViewModel.DashboardCommand.CanExecute(null) ? () => ViewModel.DashboardCommand.Execute(null) : null,
            openStatusPage: ViewModel.StatusPageCommand.CanExecute(null) ? () => ViewModel.StatusPageCommand.Execute(null) : null,
            openAddAccount: ViewModel.AddAccountCommand.CanExecute(null) ? () => ViewModel.AddAccountCommand.Execute(null) : null);
        if (Content is FrameworkElement root)
        {
            root.DataContext = ViewModel;
        }
    }

    public PopoverWindow(PopoverViewModel viewModel, ThemeListener theme)
    {
        ViewModel = viewModel;
        InitializeComponent();

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(440, 520));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(true, false);
        }

        TrySetBackdrop();
        ApplyTheme(theme.Effective);
        theme.Changed += (_, _) => DispatcherQueue.TryEnqueue(() => ApplyTheme(theme.Effective));

        SetActiveProviderInUI(viewModel.ActiveProvider);

        indicatorTimer = DispatcherQueue.CreateTimer();
        indicatorTimer.Interval = TimeSpan.FromSeconds(5);
        indicatorTimer.Tick += (_, _) => ViewModel.RefreshLiveIndicator();
        indicatorTimer.Start();

        Closed += (_, _) =>
        {
            indicatorTimer?.Stop();
            indicatorTimer = null;
            acrylicController?.Dispose();
            acrylicController = null;
            micaController?.Dispose();
            micaController = null;
            backdropConfig = null;
        };
    }

    private void TrySetBackdrop()
    {
        backdropConfig = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };

        // Prefer Desktop Acrylic — the translucent flyout material used by Start menu,
        // taskbar overflow, etc. Fall back to Mica on systems where Acrylic isn't supported.
        if (DesktopAcrylicController.IsSupported())
        {
            acrylicController = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Default };
            acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            acrylicController.SetSystemBackdropConfiguration(backdropConfig);
            return;
        }

        if (MicaController.IsSupported())
        {
            micaController = new MicaController { Kind = MicaKind.BaseAlt };
            micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
            micaController.SetSystemBackdropConfiguration(backdropConfig);
        }
    }

    private void ApplyTheme(CodexBarTheme effective)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = effective == CodexBarTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        }
        if (backdropConfig is not null)
        {
            backdropConfig.Theme = effective == CodexBarTheme.Dark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        }
    }

    private void ProviderNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            if (Enum.TryParse<UsageProvider>(tag, ignoreCase: false, out var provider))
            {
                ViewModel.SelectProvider(provider);
            }
        }
    }

    private void SetActiveProviderInUI(UsageProvider provider)
    {
        foreach (var obj in ProviderNav.MenuItems)
        {
            if (obj is NavigationViewItem item && item.Tag is string tag && tag == provider.ToString())
            {
                ProviderNav.SelectedItem = item;
                return;
            }
        }
    }
}
