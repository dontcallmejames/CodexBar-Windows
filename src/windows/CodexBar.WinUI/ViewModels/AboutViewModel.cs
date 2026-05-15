using CodexBar.Core.Updates;

namespace CodexBar.WinUI.ViewModels;

public sealed class AboutViewModel
{
    public AboutViewModel(AppVersionInfo version)
    {
        DisplayVersion = version.CurrentTag;
        Channel = version.Channel;
    }
    public string DisplayVersion { get; }
    public string Channel { get; }
}
