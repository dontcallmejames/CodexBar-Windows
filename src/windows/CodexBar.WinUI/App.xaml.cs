using Microsoft.UI.Xaml;

namespace CodexBar.WinUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Spike: no UI yet. Subsequent tasks will create tray + popover.
    }
}
