using CodexBar.Core.Updates;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace CodexBar.WinUI.Services;

public static class UpdateNotificationPoster
{
    public static void Show(UpdateCheckResult result)
    {
        var builder = new AppNotificationBuilder()
            .AddText("CodexBar update available")
            .AddText(result.LatestTag is not null
                ? $"{result.LatestTag} is now available."
                : result.StatusText);

        if (result.ReleaseUri is not null)
        {
            var openReleaseButton = new AppNotificationButton("Open release")
                .AddArgument("action", "open-release")
                .AddArgument("url", result.ReleaseUri.ToString());
            builder.AddButton(openReleaseButton);
        }

        var notification = builder.BuildNotification();
        AppNotificationManager.Default.Show(notification);
    }
}
