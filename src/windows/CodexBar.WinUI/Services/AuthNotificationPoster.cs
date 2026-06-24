using CodexBar.Core.Models;
using CodexBar.Core.Providers;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace CodexBar.WinUI.Services;

public static class AuthNotificationPoster
{
    public static void Show(UsageProvider provider, string displayName, string message)
    {
        var builder = new AppNotificationBuilder()
            .AddText($"{displayName} needs you to sign in again")
            .AddText(message);

        var howToButton = new AppNotificationButton("How to reconnect")
            .AddArgument("action", "open-setup")
            .AddArgument("url", ProviderLinks.SetupUri(provider).ToString());
        builder.AddButton(howToButton);

        var notification = builder.BuildNotification();
        AppNotificationManager.Default.Show(notification);
    }
}
