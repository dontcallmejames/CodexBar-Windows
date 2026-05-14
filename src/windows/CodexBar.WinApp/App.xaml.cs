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

    /// <summary>
    /// Calculates the screen position for the settings/first-run window relative to an anchor.
    /// Kept here as a public static so existing tests can reference it without changes.
    /// </summary>
    public static (double Left, double Top) CalculateSettingsPosition(
        double settingsWidth,
        double settingsHeight,
        double anchorLeft,
        double anchorTop,
        double anchorWidth,
        double anchorHeight,
        System.Windows.Rect workArea)
    {
        const double margin = 16;
        const double gap = 12;
        var rightCandidate = anchorLeft + anchorWidth + gap;
        var leftCandidate = anchorLeft - settingsWidth - gap;
        var maxLeft = workArea.Right - settingsWidth - margin;
        var left = rightCandidate <= maxLeft ? rightCandidate : leftCandidate;
        var anchorCenter = anchorTop + (anchorHeight / 2);
        var top = anchorCenter - (settingsHeight / 2);
        var maxTop = workArea.Bottom - settingsHeight - margin;

        return (
            Math.Clamp(left, workArea.Left + margin, maxLeft),
            Math.Clamp(top, workArea.Top + margin, maxTop));
    }
}
