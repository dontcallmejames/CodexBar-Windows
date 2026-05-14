using System;
using System.IO;
using System.Windows.Threading;
using CodexBar.WinApp.Services;

namespace CodexBar.WinApp;

public partial class App : System.Windows.Application
{
    private AppShellController? controller;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        controller = await AppHostBuilder.BuildAsync();
        await controller.StartAsync(default);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        controller?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteCrashLog("AppDomainUnhandled", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("UnobservedTask", e.Exception);
        e.SetObserved();
    }

    private static void WriteCrashLog(string source, Exception? error)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "codexbar-crash.log");
            var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(path, $"[{stamp}] {source}: {error}\n\n");
        }
        catch { /* never let logger throw */ }
    }
}
