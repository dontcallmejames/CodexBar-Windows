using System;
using System.Diagnostics;

namespace CodexBar.WinUI.Services;

/// <summary>
/// Safe wrapper for launching external URLs. Only opens absolute http/https URIs so a
/// malicious or malformed string can't be coerced into launching a local file/scheme.
/// </summary>
public static class ExternalLauncher
{
    public static void OpenExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenExternalUrl failed for {uri}: {ex}");
        }
    }

    public static void OpenExternalUrl(Uri? uri)
    {
        if (uri is null) return;
        OpenExternalUrl(uri.AbsoluteUri);
    }
}
