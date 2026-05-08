using System.Windows;

namespace CodexBar.WinApp.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutWindowViewModel(AppVersionInfo.Current);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public sealed class AboutWindowViewModel
{
    public AboutWindowViewModel(AppVersionInfo versionInfo)
    {
        DisplayVersion = versionInfo.CurrentTag;
        Channel = versionInfo.Channel;
    }

    public string DisplayVersion { get; }
    public string Channel { get; }
}
