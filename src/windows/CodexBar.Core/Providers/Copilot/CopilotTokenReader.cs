using System.Diagnostics;

namespace CodexBar.Core.Providers.Copilot;

/// <summary>
/// Reads the GitHub PAT used for Copilot API calls by shelling out to `gh auth token`.
/// We deliberately keep this gh-CLI-only for v1 — no PAT prompt in Settings.
/// </summary>
public static class CopilotTokenReader
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(5);

    public enum TokenStatus
    {
        Ok,
        GhMissing,
        NotLoggedIn,
        Failed
    }

    public sealed record TokenResult(string? Token, TokenStatus Status, string? ErrorMessage);

    public static async Task<TokenResult> ReadAsync(CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "gh",
            Arguments = "auth token",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process? process = null;
        try
        {
            process = Process.Start(psi);
        }
        catch
        {
            return new TokenResult(null, TokenStatus.GhMissing,
                "GitHub CLI (gh) is not installed or is not on PATH. Install it from https://cli.github.com.");
        }

        if (process is null)
        {
            return new TokenResult(null, TokenStatus.GhMissing,
                "GitHub CLI (gh) is not installed or is not on PATH. Install it from https://cli.github.com.");
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ProcessTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new TokenResult(null, TokenStatus.Failed, "gh auth token timed out.");
            }

            var stdout = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            var stderr = (await process.StandardError.ReadToEndAsync(cancellationToken)).Trim();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return new TokenResult(null, TokenStatus.NotLoggedIn,
                    "Run `gh auth login` to sign in to GitHub.");
            }

            return new TokenResult(stdout, TokenStatus.Ok, null);
        }
        finally
        {
            process.Dispose();
        }
    }
}
