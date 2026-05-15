using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace CodexBar.WinUI;

public static class Program
{
    // Single-instance key — must be unique to this app.
    private const string SingleInstanceKey = "CodexBar.WinUI.SingleInstance";

    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // IMPORTANT: subscribe to NotificationInvoked BEFORE Register() — the SDK throws
        // "Must register event handlers before calling Register()" otherwise.
        Microsoft.Windows.AppNotifications.AppNotificationManager.Default.NotificationInvoked
            += App.OnNotificationInvoked;
        Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register();

        // If another instance is already running, redirect activation to it and exit.
        // To verify single-instance behavior manually:
        //   1. Launch CodexBar.WinUI.exe (instance A starts, tray icon appears).
        //   2. Launch CodexBar.WinUI.exe again (instance B detects instance A is current,
        //      redirects activation, and exits immediately).
        //   3. Confirm only one tray icon / process remains in Task Manager.
        if (DecideRedirection())
        {
            return 0;
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }

    /// <returns>
    /// <see langword="true"/> if this process is a duplicate instance and should exit;
    /// <see langword="false"/> if this process is the primary instance and should continue.
    /// </returns>
    private static bool DecideRedirection()
    {
        var keyInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
        if (keyInstance.IsCurrent)
        {
            return false;
        }

        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        // Bound the wait so a stuck primary instance can't block this redirector forever.
        // GetAwaiter().GetResult() would hang indefinitely; Wait(timeout) returns false on timeout.
        var redirectTask = keyInstance.RedirectActivationToAsync(activatedArgs).AsTask();
        if (!redirectTask.Wait(TimeSpan.FromSeconds(10)))
        {
            System.Diagnostics.Debug.WriteLine("CodexBar: RedirectActivationToAsync timed out after 10s. Exiting duplicate instance.");
        }
        return true;
    }
}
