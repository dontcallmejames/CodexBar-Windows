using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace CodexBar.Core.Updates;

public sealed record UpdateInstallerPrepareResult(
    bool Success,
    string? LocalInstallerPath,
    string? ErrorMessage)
{
    public static UpdateInstallerPrepareResult Ok(string localPath) => new(true, localPath, null);
    public static UpdateInstallerPrepareResult Failure(string message) => new(false, null, message);
}

public interface IUpdateInstaller
{
    /// <summary>
    /// Downloads the installer to a temp file and verifies its SHA-256 against the .sha256 sidecar.
    /// Returns the local path on success. Wraps all exceptions in the result — never throws
    /// except for OperationCanceledException.
    /// </summary>
    Task<UpdateInstallerPrepareResult> PrepareAsync(
        Uri installerUri,
        Uri sha256Uri,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}

public sealed class UpdateInstaller : IUpdateInstaller
{
    private readonly HttpClient httpClient;
    private readonly AppVersionInfo versionInfo;

    public UpdateInstaller(HttpClient httpClient, AppVersionInfo versionInfo)
    {
        this.httpClient = httpClient;
        this.versionInfo = versionInfo;
    }

    public async Task<UpdateInstallerPrepareResult> PrepareAsync(
        Uri installerUri,
        Uri sha256Uri,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        string? localPath = null;
        try
        {
            // Random filename so concurrent prepare calls don't clobber each other and so an
            // abandoned partial download doesn't get reused as a "good" file next time.
            localPath = Path.Combine(
                Path.GetTempPath(),
                $"CodexBar-Windows-update-{Guid.NewGuid():N}.installer.exe");

            await DownloadAsync(installerUri, localPath, progress, cancellationToken);

            var expected = await FetchExpectedSha256Async(sha256Uri, cancellationToken);
            if (expected is null)
            {
                TryDelete(localPath);
                return UpdateInstallerPrepareResult.Failure("Could not read the SHA-256 sidecar.");
            }

            var actual = await ComputeSha256HexAsync(localPath, cancellationToken);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(localPath);
                return UpdateInstallerPrepareResult.Failure(
                    "Downloaded installer failed SHA-256 verification.");
            }

            return UpdateInstallerPrepareResult.Ok(localPath);
        }
        catch (OperationCanceledException)
        {
            if (localPath is not null) TryDelete(localPath);
            throw;
        }
        catch (Exception ex)
        {
            if (localPath is not null) TryDelete(localPath);
            return UpdateInstallerPrepareResult.Failure(ex.Message);
        }
    }

    private async Task DownloadAsync(Uri uri, string localPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CodexBar-Windows", versionInfo.DisplayVersion));
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Installer download failed: {(int)response.StatusCode} {response.ReasonPhrase}".Trim());
        }

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long written = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;
            if (progress is not null && totalBytes is > 0)
            {
                progress.Report(Math.Clamp((double)written / totalBytes.Value, 0.0, 1.0));
            }
        }

        if (progress is not null) progress.Report(1.0);
    }

    private async Task<string?> FetchExpectedSha256Async(Uri sha256Uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, sha256Uri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CodexBar-Windows", versionInfo.DisplayVersion));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Sidecar format: "<hex>  <filename>" — take the first whitespace-separated token.
        var firstToken = text.AsSpan().Trim();
        var space = firstToken.IndexOfAny(" \t\r\n");
        if (space > 0) firstToken = firstToken[..space];
        return firstToken.IsEmpty ? null : firstToken.ToString();
    }

    private static async Task<string> ComputeSha256HexAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
