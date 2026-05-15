using CodexBar.WinUI.ViewModels;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace CodexBar.WinUI.Views;

public sealed partial class TaskbarDockWindow : Window
{
    private DesktopAcrylicController? acrylicController;
    private SystemBackdropConfiguration? backdropConfig;

    public TaskbarDockViewModel ViewModel { get; private set; }

    public TaskbarDockWindow(TaskbarDockViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(400, 84));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(true, false);
        }

        TrySetAcrylic();

        Closed += (_, _) =>
        {
            acrylicController?.Dispose();
            acrylicController = null;
            backdropConfig = null;
        };
    }

    /// <summary>
    /// Reconcile the existing VM's tile collection in place. We MUST NOT replace ViewModel
    /// itself — the XAML's {x:Bind ViewModel.Tiles} binds to the original instance at
    /// compile time and won't follow a reference swap.
    /// </summary>
    public void ReconcileFrom(System.Collections.Generic.IReadOnlyList<CodexBar.Core.Models.UsageSnapshot> snapshots, bool showUsageAsUsed)
    {
        ViewModel.ReconcileFrom(snapshots, showUsageAsUsed);
    }

    private void TrySetAcrylic()
    {
        if (!DesktopAcrylicController.IsSupported()) return;

        backdropConfig = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };
        acrylicController = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Default };
        acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
        acrylicController.SetSystemBackdropConfiguration(backdropConfig);
    }
}
