using CodexBar.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace CodexBar.WinUI.Views;

public sealed partial class AboutWindow : Window
{
    public AboutViewModel ViewModel { get; }

    public AboutWindow(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(380, 340));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(true, false);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
