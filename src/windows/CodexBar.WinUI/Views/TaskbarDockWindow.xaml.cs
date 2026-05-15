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

        ExtendsContentIntoTitleBar = true;
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(460, 84));

        TrySetAcrylic();

        Closed += (_, _) =>
        {
            acrylicController?.Dispose();
            acrylicController = null;
            backdropConfig = null;
        };
    }

    public void SetViewModel(TaskbarDockViewModel viewModel)
    {
        ViewModel = viewModel;
        if (Content is FrameworkElement root)
            root.DataContext = viewModel;
        TilesRepeater.ItemsSource = viewModel.Tiles;
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
