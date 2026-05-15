using Microsoft.Win32;

namespace CodexBar.WinApp;

public interface IStartupRegistration
{
    void SetEnabled(bool enabled);
}

public sealed class StartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexBar";
    private readonly string executablePath;

    public StartupRegistration(string executablePath)
    {
        this.executablePath = executablePath;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
        {
            throw new InvalidOperationException("Could not open the Windows startup registry key.");
        }

        if (enabled)
        {
            key.SetValue(ValueName, BuildRunValue(executablePath), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static string BuildRunValue(string executablePath) => $"\"{executablePath}\"";
}
