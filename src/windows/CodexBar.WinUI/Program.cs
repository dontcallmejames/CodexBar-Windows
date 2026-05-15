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
        keyInstance.RedirectActivationToAsync(activatedArgs).AsTask().GetAwaiter().GetResult();
        return true;
    }
}
