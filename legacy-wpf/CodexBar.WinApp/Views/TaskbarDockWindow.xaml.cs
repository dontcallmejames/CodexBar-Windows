using System.Windows;
using System.Windows.Input;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.WinApp.Views;

public partial class TaskbarDockWindow : Window
{
    private readonly Action openPopover;
    private readonly Action refresh;
    private readonly Action openSettings;
    private readonly Action hideDock;

    public TaskbarDockWindow(
        TaskbarDockViewModel viewModel,
        Action openPopover,
        Action refresh,
        Action openSettings,
        Action hideDock)
    {
        InitializeComponent();
        DataContext = viewModel;
        this.openPopover = openPopover;
        this.refresh = refresh;
        this.openSettings = openSettings;
        this.hideDock = hideDock;
    }

    private void Dock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        openPopover();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        refresh();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        openSettings();
    }

    private void HideDock_Click(object sender, RoutedEventArgs e)
    {
        hideDock();
    }
}
