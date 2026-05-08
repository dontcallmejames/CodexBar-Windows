using System.Windows;
using CodexBar.Core.Settings;
using CodexBar.Core.Paths;
using CodexBar.Core.Models;
using CodexBar.WinApp.Settings;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.WinApp.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsWriter settingsWriter;

    public SettingsWindow(
        AppSettings settings,
        JsonSettingsStore store,
        IAppPaths? paths = null,
        IReadOnlyList<UsageSnapshot>? snapshots = null,
        AppVersionInfo? versionInfo = null,
        UpdateCheckResult? updateStatus = null)
        : this(settings, new JsonSettingsWriter(store), paths, snapshots, versionInfo, updateStatus)
    {
    }

    public SettingsWindow(
        AppSettings settings,
        ISettingsWriter settingsWriter,
        IAppPaths? paths = null,
        IReadOnlyList<UsageSnapshot>? snapshots = null,
        AppVersionInfo? versionInfo = null,
        UpdateCheckResult? updateStatus = null)
    {
        InitializeComponent();
        this.settingsWriter = settingsWriter;
        DataContext = new SettingsViewModel(settings, paths, snapshots, versionInfo, updateStatus);
    }

    public event EventHandler<AppSettings>? SettingsSaved;
    public event EventHandler? BugReportRequested;
    public event EventHandler? UpdateCheckRequested;
    public event EventHandler<UsageProvider>? TestProviderRequested;
    public event EventHandler<UsageProvider>? ProviderHelpRequested;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            var result = await SaveSettingsAsync(settingsWriter, viewModel, CancellationToken.None);
            if (!result.Succeeded)
            {
                System.Windows.MessageBox.Show(
                    this,
                    result.Error?.Message ?? "Settings could not be saved.",
                    "CodexBar Settings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            SettingsSaved?.Invoke(this, result.Settings!);
        }

        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ReportBug_Click(object sender, RoutedEventArgs e)
    {
        BugReportRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        UpdateCheckRequested?.Invoke(this, EventArgs.Empty);
    }

    private void TestProvider_Click(object sender, RoutedEventArgs e)
    {
        if (ProviderFromSender(sender) is { } provider)
        {
            TestProviderRequested?.Invoke(this, provider);
        }
    }

    private void ProviderHelp_Click(object sender, RoutedEventArgs e)
    {
        if (ProviderFromSender(sender) is { } provider)
        {
            ProviderHelpRequested?.Invoke(this, provider);
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

    public static async Task<SettingsSaveResult> SaveSettingsAsync(
        ISettingsWriter writer,
        SettingsViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var settings = viewModel.ToSettings();
        try
        {
            await writer.SaveAsync(settings, cancellationToken);
            return SettingsSaveResult.Success(settings);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return SettingsSaveResult.Failure(error);
        }
    }
}

public sealed record SettingsSaveResult(bool Succeeded, AppSettings? Settings, Exception? Error)
{
    public static SettingsSaveResult Success(AppSettings settings) => new(true, settings, null);
    public static SettingsSaveResult Failure(Exception error) => new(false, null, error);
}
