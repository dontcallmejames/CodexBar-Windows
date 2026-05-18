using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CodexBar.Core.Updates;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string? LatestTag,
    Uri? ReleaseUri,
    string StatusText,
    string? ErrorMessage,
    Uri? InstallerAssetUri = null,
    Uri? InstallerSha256Uri = null)
{
    public static UpdateCheckResult Available(string latestTag, Uri releaseUri, Uri? installerAssetUri = null, Uri? installerSha256Uri = null) =>
        new(true, latestTag, releaseUri, $"Update available: {latestTag}", null, installerAssetUri, installerSha256Uri);

    public static UpdateCheckResult UpToDate(string? latestTag) =>
        new(false, latestTag, null, latestTag is null ? "No release metadata found." : "You're on the latest release.", null);

    public static UpdateCheckResult Failed(string message) =>
        new(false, null, null, "Update check failed. Open Releases to check manually.", message);
}

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken);
}

public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private static readonly Uri ReleasesApiUri = new("https://api.github.com/repos/dontcallmejames/CodexBar-Windows/releases?per_page=20");
    private static readonly Uri FallbackReleasesUri = new("https://github.com/dontcallmejames/CodexBar-Windows/releases");
    private readonly HttpClient httpClient;
    private readonly AppVersionInfo versionInfo;

    public GitHubUpdateChecker(HttpClient httpClient, AppVersionInfo versionInfo)
    {
        this.httpClient = httpClient;
        this.versionInfo = versionInfo;
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CodexBar-Windows", versionInfo.DisplayVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed(
                    $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim());
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var release = FindLatestRelease(json.RootElement);
            if (release is null)
            {
                return UpdateCheckResult.UpToDate(null);
            }

            var tag = ReadString(release.Value, "tag_name");
            var uriText = ReadString(release.Value, "html_url");
            var releaseUri = string.IsNullOrWhiteSpace(uriText) ? FallbackReleasesUri : new Uri(uriText);
            var (installerUri, sha256Uri) = ExtractInstallerAssets(release.Value);

            return versionInfo.IsOlderThan(tag)
                ? UpdateCheckResult.Available(tag!, releaseUri, installerUri, sha256Uri)
                : UpdateCheckResult.UpToDate(tag);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return UpdateCheckResult.Failed(error.Message);
        }
    }

    private static JsonElement? FindLatestRelease(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return IsDraft(element) ? null : element;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var release in element.EnumerateArray())
        {
            if (release.ValueKind == JsonValueKind.Object && !IsDraft(release))
            {
                return release;
            }
        }

        return null;
    }

    private static bool IsDraft(JsonElement element) =>
        element.TryGetProperty("draft", out var draft) &&
        draft.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Walks the release's "assets" array looking for the Windows installer + its SHA-256 sidecar.
    /// The installer asset's "name" ends with ".installer.exe"; the sidecar ends with
    /// ".installer.exe.sha256". Returns (null, null) when either is missing — the in-app installer
    /// flow becomes disabled but the "Open release" path still works.
    /// </summary>
    private static (Uri? Installer, Uri? Sha256) ExtractInstallerAssets(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        Uri? installer = null;
        Uri? sha256 = null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object) continue;
            var name = ReadString(asset, "name");
            var downloadUrl = ReadString(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl)) continue;
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)) continue;

            // Sidecar check FIRST so ".installer.exe.sha256" doesn't get caught by the installer matcher.
            if (name.EndsWith(".installer.exe.sha256", StringComparison.OrdinalIgnoreCase))
            {
                sha256 ??= uri;
            }
            else if (name.EndsWith(".installer.exe", StringComparison.OrdinalIgnoreCase))
            {
                installer ??= uri;
            }
        }

        return (installer is not null && sha256 is not null) ? (installer, sha256) : (null, null);
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
