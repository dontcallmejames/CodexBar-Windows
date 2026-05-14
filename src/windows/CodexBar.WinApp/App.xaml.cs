using CodexBar.WinApp.Services;

namespace CodexBar.WinApp;

public partial class App : System.Windows.Application
{
    private AppShellController? controller;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        controller = await AppHostBuilder.BuildAsync();
        await controller.StartAsync(default);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        controller?.Dispose();
        base.OnExit(e);
    }
}
