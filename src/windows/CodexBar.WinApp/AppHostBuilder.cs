using CodexBar.Core.Paths;
using CodexBar.Core.Settings;
using CodexBar.WinApp.Services;

namespace CodexBar.WinApp;

public static class AppHostBuilder
{
    public static async Task<AppShellController> BuildAsync(CancellationToken cancellationToken = default)
    {
        var paths = new WindowsAppPaths();
        var settingsFilePath = paths.SettingsFile;
        var settingsStore = new JsonSettingsStore(settingsFilePath);
        var settings = await AppShellController.LoadSettingsOrDefaultAsync(paths, cancellationToken);
        var services = new AppServices(paths, settings);
        var startupRegistration = new StartupRegistration(
            Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0]);

        return new AppShellController(
            services,
            settingsStore,
            settingsFilePath,
            startupRegistration);
    }
}
