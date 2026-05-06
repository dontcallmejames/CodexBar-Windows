using System.Windows;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.WinApp.Views;

public partial class DockedOverviewWindow : Window
{
    public DockedOverviewWindow(DockedOverviewViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
