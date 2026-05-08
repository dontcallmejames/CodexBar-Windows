using CodexBar.Core.Settings;
using CodexBar.Core.Paths;
using CodexBar.WinApp.Settings;
using CodexBar.WinApp.ViewModels;
using CodexBar.WinApp.Views;

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

        StringAssert.Contains(settingsXaml, "Check for Updates...");
        StringAssert.Contains(settingsXaml, "Click=\"CheckUpdates_Click\"");
        StringAssert.Contains(settingsCode, "UpdateCheckRequested");
        StringAssert.Contains(settingsCode, "CheckUpdates_Click");
    }

    private sealed class ThrowingSettingsWriter : ISettingsWriter
    {
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("save failed");
    }
}
