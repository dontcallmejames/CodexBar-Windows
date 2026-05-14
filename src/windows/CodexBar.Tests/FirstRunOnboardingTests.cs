using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using CodexBar.WinApp;
using CodexBar.WinApp.ViewModels;

namespace CodexBar.Tests;

[TestClass]
public sealed class FirstRunOnboardingTests
{
    [TestMethod]
    public void ShowsFirstRunOnboardingOnlyWhenSettingsFileIsMissing()
    {
        Assert.IsTrue(App.ShouldShowFirstRunOnboarding(settingsFileExists: false));
        Assert.IsFalse(App.ShouldShowFirstRunOnboarding(settingsFileExists: true));
    }

    [TestMethod]
    public void FirstRunViewModelReportsProviderCredentialStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var paths = WindowsAppPaths.ForTest(Path.Combine(root, "home"), Path.Combine(root, "appdata"));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.CodexAuthJson(null))!);
            Directory.CreateDirectory(Path.GetDirectoryName(paths.GeminiOAuthCredentialsJson)!);
            File.WriteAllText(paths.CodexAuthJson(null), "{}");
            File.WriteAllText(paths.GeminiOAuthCredentialsJson, "{}");

            var viewModel = new FirstRunViewModel(AppSettings.Default, paths);

            Assert.AreEqual("Connected", viewModel.CodexAccountStatus);
            Assert.AreEqual("Not connected", viewModel.ClaudeAccountStatus);
            Assert.AreEqual("Not connected", viewModel.CursorAccountStatus);
            Assert.AreEqual("Connected", viewModel.GeminiAccountStatus);
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
    public void FirstRunViewModelSavesProviderSelectionsWithoutChangingOtherSettings()
    {
        var original = AppSettings.Default with
        {
            DockOverviewNearTaskbar = true,
            RefreshMinutes = 9,
            ClaudeManualCookieHeader = "sessionKey=abc"
        };
        var viewModel = new FirstRunViewModel(original)
        {
            CodexEnabled = true,
            ClaudeEnabled = false,
            CursorEnabled = false,
            GeminiEnabled = true
        };

        var settings = viewModel.ToSettings();

        Assert.IsTrue(settings.CodexEnabled);
        Assert.IsFalse(settings.ClaudeEnabled);
        Assert.IsFalse(settings.CursorEnabled);
        Assert.IsTrue(settings.GeminiEnabled);
        Assert.IsTrue(settings.DockOverviewNearTaskbar);
        Assert.AreEqual(9, settings.RefreshMinutes);
        Assert.AreEqual("sessionKey=abc", settings.ClaudeManualCookieHeader);
    }

    [TestMethod]
    public void FirstRunViewModelUsesLatestProviderSnapshotsForStatusAndDetails()
    {
        var snapshots = new[]
        {
            new UsageSnapshot(
                UsageProvider.Claude,
                "Claude",
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
                false),
            UsageSnapshot.MissingCredentials(
                UsageProvider.Cursor,
                "Cursor",
                "Cursor manual cookie header is missing.")
        };

        var viewModel = new FirstRunViewModel(AppSettings.Default, snapshots: snapshots);

        Assert.AreEqual("No usage yet", viewModel.ClaudeAccountStatus);
        Assert.AreEqual("Credentials were found, but Claude did not return usage windows yet.", viewModel.ClaudeAccountDetail);
        Assert.AreEqual("Needs attention", viewModel.CursorAccountStatus);
        Assert.AreEqual("Cursor manual cookie header is missing.", viewModel.CursorAccountDetail);
    }

    [TestMethod]
    public void FirstRunWindowExposesProviderSetupControls()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "FirstRunWindow.xaml"));
        var codePath = Path.ChangeExtension(xamlPath, ".xaml.cs");

        var xaml = File.ReadAllText(xamlPath);
        var code = File.ReadAllText(codePath);

        StringAssert.Contains(xaml, "Welcome to CodexBar");
        StringAssert.Contains(xaml, "Enable Codex");
        StringAssert.Contains(xaml, "Enable Claude");
        StringAssert.Contains(xaml, "Enable Cursor");
        StringAssert.Contains(xaml, "Enable Gemini");
        StringAssert.Contains(xaml, "Help");
        StringAssert.Contains(xaml, "Get Started");
        StringAssert.Contains(xaml, "Skip");
        StringAssert.Contains(code, "OnboardingSaved");
        StringAssert.Contains(code, "OnboardingSkipped");
        StringAssert.Contains(code, "ProviderHelpRequested");
    }

    [TestMethod]
    public void AppWiresFirstRunOnboardingNearAppStartup()
    {
        var appCodePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "App.xaml.cs"));
        var appCode = File.ReadAllText(appCodePath);

        var lifecycleCodePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Services",
            "WindowCoordinator.Lifecycle.cs"));
        var lifecycleCode = File.ReadAllText(lifecycleCodePath);

        StringAssert.Contains(appCode, "ShouldShowFirstRunOnboarding(settingsFileExists)");
        StringAssert.Contains(appCode, "ShowFirstRunOnboarding");
        StringAssert.Contains(lifecycleCode, "PositionWindowNearApp(firstRunWindow)");
        StringAssert.Contains(lifecycleCode, "FirstRunWindow_OnboardingSaved");
        StringAssert.Contains(lifecycleCode, "FirstRunWindow_OnboardingSkipped");
    }
}
