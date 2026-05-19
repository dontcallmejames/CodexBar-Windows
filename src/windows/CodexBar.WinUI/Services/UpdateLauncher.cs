using System;
using System.Diagnostics;

namespace CodexBar.WinUI.Services;

/// <summary>
/// Launches a prepared Inno Setup installer with arguments that auto-close the running app
/// and silent-install. Caller is expected to QuitApp() immediately after a successful launch
/// so the installer can replace the executable.
/// </summary>
public sealed class UpdateLauncher
{
    /// <summary>
    /// Starts the installer process and returns immediately. UseShellExecute=true + Verb=runas
    /// triggers UAC. <c>/CLOSEAPPLICATIONS</c> tells Inno Setup to gracefully terminate the
    /// running CodexBar before replacing files; <c>/RESTARTAPPLICATIONS</c> asks Restart
    /// Manager to relaunch them after install. <c>/SILENT</c> skips most dialogs (progress
    /// window still shows) and <c>/SUPPRESSMSGBOXES</c> skips error popups. The Inno script's
    /// [Run] entry (without <c>skipifsilent</c>) is the belt-and-suspenders that relaunches
    /// CodexBar even when Restart Manager doesn't catch it.
    /// </summary>
    public bool LaunchAndDetach(string installerPath, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /SILENT /SUPPRESSMSGBOXES",
                UseShellExecute = true,
                Verb = "runas",
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                errorMessage = "Could not start the installer process.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            // Most common case: user denied the UAC prompt (Win32Exception, code 1223).
            errorMessage = ex.Message;
            return false;
        }
    }
}
