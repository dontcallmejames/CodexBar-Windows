using CodexBar.Core.Settings;
using CodexBar.Core.Paths;
using CodexBar.Core.Models;
using CodexBar.WinApp;
using CodexBar.WinApp.Settings;
using CodexBar.WinApp.ViewModels;
using CodexBar.WinApp.Views;
using System.ComponentModel;

namespace CodexBar.Tests;

[TestClass]
public sealed class SettingsWindowTests
{
    [TestMethod]
    public async Task SaveSettingsAsyncReturnsFailureWhenWriterThrows()
    {
        var writer = new ThrowingSettingsWriter();
        var viewModel = new SettingsViewModel(AppSettings.Default);

        var result = await SettingsWindow.SaveSettingsAsync(writer, viewModel, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsInstanceOfType<InvalidOperationException>(result.Error);
    }

    [TestMethod]
    public void SettingsViewModelReportsProviderCredentialStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.CodexAuthJson(null))!);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.ClaudeCredentialsJson)!);
            File.WriteAllText(paths.CodexAuthJson(null), "{}");

            var viewModel = new SettingsViewModel(AppSettings.Default with { LaunchAtStartup = true }, paths);

            Assert.IsTrue(viewModel.LaunchAtStartup);
            Assert.AreEqual("Connected", viewModel.CodexAccountStatus);
            Assert.AreEqual("Not connected", viewModel.ClaudeAccountStatus);
            Assert.AreEqual(paths.CodexAuthJson(null), viewModel.CodexCredentialPath);
            Assert.AreEqual(paths.ClaudeCredentialsJson, viewModel.ClaudeCredentialPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void SettingsViewModelReportsCursorAndGeminiCredentialStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
            File.WriteAllText(paths.GeminiOAuthCredentialsJson, "{}");

            var settings = AppSettings.Default with { CursorManualCookieHeader = "WorkosCursorSessionToken=abc" };
            var viewModel = new SettingsViewModel(settings, paths);

            Assert.IsTrue(viewModel.CursorEnabled);
            Assert.IsTrue(viewModel.GeminiEnabled);
            Assert.AreEqual("Connected", viewModel.CursorAccountStatus);
            Assert.AreEqual("Connected", viewModel.GeminiAccountStatus);
            Assert.AreEqual(paths.GeminiOAuthCredentialsJson, viewModel.GeminiCredentialPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void SettingsViewModelRaisesPropertyChangedForMutableSettings()
    {
        var viewModel = new SettingsViewModel(AppSettings.Default);
        var changed = new List<string>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, args) => changed.Add(args.PropertyName!);

        viewModel.CodexEnabled = false;
        viewModel.RefreshMinutes = 12;
        viewModel.ClaudeManualCookieHeader = "sessionKey=abc";

        CollectionAssert.Contains(changed, nameof(SettingsViewModel.CodexEnabled));
        CollectionAssert.Contains(changed, nameof(SettingsViewModel.RefreshMinutes));
        CollectionAssert.Contains(changed, nameof(SettingsViewModel.ClaudeManualCookieHeader));
    }

    [TestMethod]
    public void SettingsViewModelUsesLatestProviderSnapshotsForStatusAndDetails()
    {
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Codex,
                "Codex",
                DateTimeOffset.Now,
                new[] { new RateWindow("weekly", "Weekly", 25, null, null) },
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "test",
                null,
                false),
            UsageSnapshot.MissingCredentials(
                UsageProvider.Gemini,
                "Gemini",
                "Gemini CLI OAuth credentials were not found.")
        };

        var viewModel = new SettingsViewModel(AppSettings.Default, snapshots: snapshots);

        Assert.AreEqual("Connected", viewModel.CodexAccountStatus);
        Assert.AreEqual("Usage data refreshed successfully.", viewModel.CodexAccountDetail);
        Assert.AreEqual("Needs attention", viewModel.GeminiAccountStatus);
        Assert.AreEqual("Gemini CLI OAuth credentials were not found.", viewModel.GeminiAccountDetail);
    }

    [TestMethod]
    public void SettingsViewModelReportsNoUsageYetWhenCredentialsReturnNoWindows()
    {
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Gemini,
                "Gemini",
                DateTimeOffset.Now,
                Array.Empty<RateWindow>(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "oauth",
                null,
                false)
        };

        var viewModel = new SettingsViewModel(AppSettings.Default, snapshots: snapshots);

        Assert.AreEqual("No usage yet", viewModel.GeminiAccountStatus);
        Assert.AreEqual("Credentials were found, but Gemini did not return usage windows yet.", viewModel.GeminiAccountDetail);
    }

    [TestMethod]
    public void SettingsWindowCancelButtonHasCloseHandler()
    {
        var settingsXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "SettingsWindow.xaml"));
        var settingsCodePath = Path.ChangeExtension(settingsXamlPath, ".xaml.cs");

        var settingsXaml = File.ReadAllText(settingsXamlPath);
        var settingsCode = File.ReadAllText(settingsCodePath);

        StringAssert.Contains(settingsXaml, "Click=\"Cancel_Click\"");
        StringAssert.Contains(settingsCode, "private void Cancel_Click");
        StringAssert.Contains(settingsCode, "Close();");
    }

    [TestMethod]
    public void SettingsWindowLabelsDockAsTaskbarDock()
    {
        var settingsXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "SettingsWindow.xaml"));

        var settingsXaml = File.ReadAllText(settingsXamlPath);

        StringAssert.Contains(settingsXaml, "Show taskbar dock");
        StringAssert.Contains(settingsXaml, "IsChecked=\"{Binding DockOverviewNearTaskbar}\"");
    }

    [TestMethod]
    public void SettingsWindowExposesPreviewProviderControls()
    {
        var settingsXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "SettingsWindow.xaml"));

        var settingsXaml = File.ReadAllText(settingsXamlPath);

        StringAssert.Contains(settingsXaml, "Enable Cursor");
        StringAssert.Contains(settingsXaml, "Enable Gemini");
        StringAssert.Contains(settingsXaml, "Cursor manual cookie header");
        StringAssert.Contains(settingsXaml, "GeminiAccountStatus");
    }

    [TestMethod]
    public void SettingsWindowExposesBugReportButton()
    {
        var settingsXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "SettingsWindow.xaml"));
        var settingsCodePath = Path.ChangeExtension(settingsXamlPath, ".xaml.cs");

        var settingsXaml = File.ReadAllText(settingsXamlPath);
        var settingsCode = File.ReadAllText(settingsCodePath);

        StringAssert.Contains(settingsXaml, "Report a Bug...");
        StringAssert.Contains(settingsXaml, "Click=\"ReportBug_Click\"");
        StringAssert.Contains(settingsCode, "BugReportRequested");
        StringAssert.Contains(settingsCode, "ReportBug_Click");
    }

    [TestMethod]
    public void SettingsWindowExposesUpdateCheckButton()
    {
        var settingsXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "SettingsWindow.xaml"));
        var settingsCodePath = Path.ChangeExtension(settingsXamlPath, ".xaml.cs");

        var settingsXaml = File.ReadAllText(settingsXamlPath);
        var settingsCode = File.ReadAllText(settingsCodePath);

        StringAssert.Contains(settingsXaml, "UpdateActionText");
        StringAssert.Contains(settingsXaml, "Click=\"CheckUpdates_Click\"");
        StringAssert.Contains(settingsCode, "UpdateCheckRequested");
        StringAssert.Contains(settingsCode, "CheckUpdates_Click");
    }

    [TestMethod]
    public void SettingsWindowShowsVersionAndUpdateStatus()
    {
        var settingsXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "SettingsWindow.xaml"));

        var settingsXaml = File.ReadAllText(settingsXamlPath);

        StringAssert.Contains(settingsXaml, "CurrentVersionText");
        StringAssert.Contains(settingsXaml, "LatestVersionText");
        StringAssert.Contains(settingsXaml, "UpdateStatusText");
        StringAssert.Contains(settingsXaml, "UpdateActionText");

        var viewModel = new SettingsViewModel(
            AppSettings.Default,
            versionInfo: AppVersionInfo.FromMarketingVersion(
                "0.25",
                buildNumber: "60",
                windowsPreviewNumber: "3"),
            updateStatus: UpdateCheckResult.Available(
                "v0.25.0-preview.4",
                new Uri("https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.4")));

        Assert.AreEqual("Version v0.25.0-preview.3", viewModel.CurrentVersionText);
        Assert.AreEqual("Latest v0.25.0-preview.4", viewModel.LatestVersionText);
        Assert.AreEqual("Update available: v0.25.0-preview.4", viewModel.UpdateStatusText);
        Assert.AreEqual("Open Release...", viewModel.UpdateActionText);
    }

    [TestMethod]
    public void SettingsViewModelUsesFriendlyUpdateStatusText()
    {
        var current = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            windowsPreviewNumber: "4");

        var upToDate = new SettingsViewModel(
            AppSettings.Default,
            versionInfo: current,
            updateStatus: UpdateCheckResult.UpToDate("v0.25.0-preview.4"));
        var failed = new SettingsViewModel(
            AppSettings.Default,
            versionInfo: current,
            updateStatus: UpdateCheckResult.Failed("404 raw status"));

        Assert.AreEqual("Latest v0.25.0-preview.4", upToDate.LatestVersionText);
        Assert.AreEqual("You're on the latest release.", upToDate.UpdateStatusText);
        Assert.AreEqual("Check for Updates...", upToDate.UpdateActionText);
        Assert.AreEqual("Latest not checked", failed.LatestVersionText);
        Assert.AreEqual("Update check failed. Open Releases to check manually.", failed.UpdateStatusText);
        Assert.AreEqual("Open Releases...", failed.UpdateActionText);
    }

    [TestMethod]
    public void SettingsWindowExposesProviderTestAndHelpButtons()
    {
        var settingsXamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "SettingsWindow.xaml"));
        var settingsCodePath = Path.ChangeExtension(settingsXamlPath, ".xaml.cs");

        var settingsXaml = File.ReadAllText(settingsXamlPath);
        var settingsCode = File.ReadAllText(settingsCodePath);

        StringAssert.Contains(settingsXaml, "TestProvider_Click");
        StringAssert.Contains(settingsXaml, "ProviderHelp_Click");
        StringAssert.Contains(settingsXaml, "Tag=\"Codex\"");
        StringAssert.Contains(settingsXaml, "Tag=\"Claude\"");
        StringAssert.Contains(settingsXaml, "Tag=\"Cursor\"");
        StringAssert.Contains(settingsXaml, "Tag=\"Gemini\"");
        StringAssert.Contains(settingsCode, "TestProviderRequested");
        StringAssert.Contains(settingsCode, "ProviderHelpRequested");
    }

    private sealed class ThrowingSettingsWriter : ISettingsWriter
    {
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("save failed");
    }
}
