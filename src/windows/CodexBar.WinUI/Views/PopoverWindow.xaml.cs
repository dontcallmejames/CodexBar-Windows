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
    private MicaController? micaController;
    private SystemBackdropConfiguration? backdropConfig;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? indicatorTimer;

    public PopoverViewModel ViewModel { get; }

    public PopoverWindow(PopoverViewModel viewModel, ThemeListener theme)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(380, 480));

        TrySetMica();
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
            micaController?.Dispose();
            micaController = null;
            backdropConfig = null;
        };
    }

    private void TrySetMica()
    {
        if (!MicaController.IsSupported())
        {
            return;
        }
        backdropConfig = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };
        micaController = new MicaController { Kind = MicaKind.Base };
        micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
        micaController.SetSystemBackdropConfiguration(backdropConfig);
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
