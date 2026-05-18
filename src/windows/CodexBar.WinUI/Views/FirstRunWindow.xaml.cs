using System;
using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using CodexBar.Core.Settings;
using CodexBar.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace CodexBar.WinUI.Views;

public sealed partial class FirstRunWindow : Window
{
    public FirstRunViewModel ViewModel { get; }
    private readonly Action<AppSettings> onGetStarted;
    private readonly Action onSkip;

    public FirstRunWindow(FirstRunViewModel viewModel, Action<AppSettings> onGetStarted, Action onSkip)
    {
        ViewModel = viewModel;
        this.onGetStarted = onGetStarted;
        this.onSkip = onSkip;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(520, 600));
    }

    private void GetStarted_Click(object sender, RoutedEventArgs e)
    {
        onGetStarted(ViewModel.ToSettings());
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        onSkip();
        Close();
    }

    private void CodexHelp_Click(object sender, RoutedEventArgs e) => OpenUri(ProviderLinks.SetupUri(UsageProvider.Codex));
    private void ClaudeHelp_Click(object sender, RoutedEventArgs e) => OpenUri(ProviderLinks.SetupUri(UsageProvider.Claude));
    private void CursorHelp_Click(object sender, RoutedEventArgs e) => OpenUri(ProviderLinks.SetupUri(UsageProvider.Cursor));
    private void GeminiHelp_Click(object sender, RoutedEventArgs e) => OpenUri(ProviderLinks.SetupUri(UsageProvider.Gemini));
    private void CopilotHelp_Click(object sender, RoutedEventArgs e) => OpenUri(ProviderLinks.SetupUri(UsageProvider.Copilot));

    private static void OpenUri(Uri uri)
    {
        Services.ExternalLauncher.OpenExternalUrl(uri);
    }
}
