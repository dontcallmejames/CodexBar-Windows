using CodexBar.Core.Models;
using CodexBar.Core.Refresh;
using CodexBar.Tray;
using CodexBar.WinApp.ViewModels;
using CodexBar.WinApp.Views;

namespace CodexBar.WinApp;

public partial class App : System.Windows.Application
{
    private readonly SnapshotStore store = new();
    private TrayIconHost? tray;
    private PopoverWindow? popover;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        SeedPreviewData();
        tray = new TrayIconHost(ShowPopover, ShowSettings, Shutdown);
        tray.Update(new TrayDisplayModel("CodexBar", 72, false));
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        tray?.Dispose();
        base.OnExit(e);
    }

    private void ShowPopover()
    {
        if (popover?.IsVisible == true)
        {
            popover.Close();
            popover = null;
            return;
        }

        var viewModel = new PopoverViewModel(
            store.All(),
            UsageProvider.Codex,
            showUsageAsUsed: true,
            openDashboard: ShowUsageDashboard,
            openSettings: ShowSettings,
            showAbout: ShowAbout,
            quit: Shutdown);
        popover = new PopoverWindow(viewModel);
        popover.Closed += (_, _) => popover = null;
        popover.Show();
        popover.Activate();
    }

    private static void ShowUsageDashboard()
    {
        const string message = "Usage dashboard actions will open provider dashboards after Task 8 wires live providers.";
        System.Windows.MessageBox.Show(message, "CodexBar");
    }

    private static void ShowSettings()
    {
        const string message = "Open settings from the tray menu after Task 9 adds the settings window.";
        System.Windows.MessageBox.Show(message, "CodexBar");
    }

    private static void ShowAbout()
    {
        const string message = "CodexBar for Windows";
        System.Windows.MessageBox.Show(message, "About CodexBar");
    }

    private void SeedPreviewData()
    {
        var now = DateTimeOffset.Now;
        store.Set(new UsageSnapshot(
            UsageProvider.Codex,
            "Codex",
            now,
            new[]
            {
                new RateWindow("session", "5-hour", 8, now.AddHours(2), 300),
                new RateWindow("weekly", "Weekly", 22, now.AddDays(3), 10080)
            },
            "dev@example.com",
            "Pro",
            42,
            0.04m,
            15000,
            254.24m,
            218000000,
            "preview",
            null,
            false));

        store.Set(new UsageSnapshot(
            UsageProvider.Claude,
            "Claude",
            now,
            new[]
            {
                new RateWindow("session", "Session", 2, now.AddHours(4), null),
                new RateWindow("weekly", "Weekly", 3, now.AddDays(4), null)
            },
            "claude@example.com",
            "Max",
            null,
            0.00m,
            null,
            12.50m,
            900000,
            "preview",
            null,
            false));
    }
}
