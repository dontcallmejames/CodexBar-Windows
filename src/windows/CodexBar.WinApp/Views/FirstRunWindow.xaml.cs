using System.Windows;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using CodexBar.WinApp.Settings;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.WinApp.Views;

public partial class FirstRunWindow : Window
{
    private readonly ISettingsWriter settingsWriter;
    private readonly AppSettings originalSettings;

    public FirstRunWindow(
        AppSettings settings,
        JsonSettingsStore store,
        IAppPaths? paths = null,
        IReadOnlyList<UsageSnapshot>? snapshots = null)
        : this(settings, new JsonSettingsWriter(store), paths, snapshots)
    {
    }

    public FirstRunWindow(
        AppSettings settings,
        ISettingsWriter settingsWriter,
        IAppPaths? paths = null,
        IReadOnlyList<UsageSnapshot>? snapshots = null)
    {
        InitializeComponent();
        this.settingsWriter = settingsWriter;
        originalSettings = settings;
        DataContext = new FirstRunViewModel(settings, paths, snapshots);
    }

    public event EventHandler<AppSettings>? OnboardingSaved;
    public event EventHandler<AppSettings>? OnboardingSkipped;
    public event EventHandler<UsageProvider>? ProviderHelpRequested;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FirstRunViewModel viewModel)
        {
            Close();
            return;
        }

        var settings = viewModel.ToSettings();
        if (await SaveAndReportAsync(settings))
        {
            OnboardingSaved?.Invoke(this, settings);
            Close();
        }
    }

    private async void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (await SaveAndReportAsync(originalSettings))
        {
            OnboardingSkipped?.Invoke(this, originalSettings);
            Close();
        }
    }

    private void ProviderHelp_Click(object sender, RoutedEventArgs e)
    {
        if (ProviderFromSender(sender) is { } provider)
        {
            ProviderHelpRequested?.Invoke(this, provider);
        }
    }

    private async Task<bool> SaveAndReportAsync(AppSettings settings)
    {
        try
        {
            await settingsWriter.SaveAsync(settings, CancellationToken.None);
            return true;
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(
                this,
                error.Message,
                "CodexBar Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private static UsageProvider? ProviderFromSender(object sender)
    {
        if (sender is not FrameworkElement { Tag: string tag })
        {
            return null;
        }

        return Enum.TryParse<UsageProvider>(tag, ignoreCase: true, out var provider)
            ? provider
            : null;
    }
}
