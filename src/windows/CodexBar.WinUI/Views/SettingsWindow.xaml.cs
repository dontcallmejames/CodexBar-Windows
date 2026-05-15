using System;
using CodexBar.Core.Settings;
using CodexBar.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace CodexBar.WinUI.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }
    private readonly Action<AppSettings> onSave;

    public SettingsWindow(SettingsViewModel viewModel, Action<AppSettings> onSave)
    {
        ViewModel = viewModel;
        this.onSave = onSave;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(540, 720));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        onSave(ViewModel.ToSettings());
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
