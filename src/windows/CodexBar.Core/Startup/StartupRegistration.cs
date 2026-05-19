using Microsoft.Win32;

namespace CodexBar.Core.Startup;

/// <summary>
/// Abstracts the per-user registry key the app uses to toggle launch-at-startup. The
/// production implementation writes to HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public interface IStartupRegistration
{
    bool IsEnabled();
    void SetEnabled(bool enabled, string exePath);
}

/// <summary>
/// Writes the CodexBar entry to HKCU\...\Run so Windows launches the app at sign-in.
/// Keeps the value simple: a quoted path to the exe, no extra command-line args. The
/// quotes matter — installer paths may contain spaces ("C:\Program Files\...").
/// </summary>
public sealed class StartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "CodexBar";

    private readonly RegistryKey rootKey;

    public StartupRegistration() : this(Registry.CurrentUser)
    {
    }

    // Public ctor used by tests so they can target a temporary HKCU subtree without
    // touching the user's real Run key. Production callers use the parameterless ctor.
    public StartupRegistration(RegistryKey rootKey)
    {
        this.rootKey = rootKey;
    }

    public bool IsEnabled()
    {
        using var key = rootKey.OpenSubKey(RunKeyPath, writable: false);
        if (key is null) return false;
        return key.GetValue(ValueName) is not null;
    }

    public void SetEnabled(bool enabled, string exePath)
    {
        using var key = rootKey.CreateSubKey(RunKeyPath, writable: true);
        if (key is null) return;

        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(exePath)) return;
            // Quote the path so installer locations with spaces work.
            key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
