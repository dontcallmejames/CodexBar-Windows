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
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(380, 340));
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
