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
    private ThemeListener? themeListener;
    private EventHandler? themeChangedHandler;

    public PopoverViewModel ViewModel { get; }

    /// <summary>
    /// Update the existing VM in place. We MUST NOT replace ViewModel itself — the XAML's
    /// {x:Bind} expressions are bound to the original VM instance at compile time and won't
    /// follow a reference swap.
    /// </summary>
    public void RefreshFromStore(
        System.Collections.Generic.IReadOnlyList<UsageSnapshot> snapshots,
        bool showUsageAsUsed)
    {
        ViewModel.UpdateSnapshots(snapshots, showUsageAsUsed);
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
        themeListener = theme;
        themeChangedHandler = (_, _) => DispatcherQueue.TryEnqueue(() => ApplyTheme(theme.Effective));
        theme.Changed += themeChangedHandler;

        SetActiveProviderInUI(viewModel.ActiveProvider);

        indicatorTimer = DispatcherQueue.CreateTimer();
        indicatorTimer.Interval = TimeSpan.FromSeconds(5);
        indicatorTimer.Tick += (_, _) => ViewModel.RefreshLiveIndicator();
        indicatorTimer.Start();

        Closed += (_, _) =>
        {
            if (themeListener is not null && themeChangedHandler is not null)
            {
                themeListener.Changed -= themeChangedHandler;
            }
            themeListener = null;
            themeChangedHandler = null;
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
