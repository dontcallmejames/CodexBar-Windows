using System;
namespace CodexBar.WinApp;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new System.Windows.Application
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
        };

        app.Shutdown();
    }
}
