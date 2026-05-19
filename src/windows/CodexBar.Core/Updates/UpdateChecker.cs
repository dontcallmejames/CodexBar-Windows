using System.Collections.Generic;
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
    /// The installer asset's "name" ends with ".installer.exe"; the sidecar's name is the
    /// installer's name plus ".sha256". When a release publishes multiple installer variants
    /// (e.g. x64 + arm64), we MUST pair each installer with its own sidecar by basename —
    /// otherwise SHA-256 verification fails despite valid assets. Returns (null, null) when
    /// no installer is found, or when the matching sidecar is missing.
    /// </summary>
    private static (Uri? Installer, Uri? Sha256) ExtractInstallerAssets(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        // Index all assets by name first so we can do an exact-pair lookup. Asset names
        // are unique within a release, so a plain dictionary is fine.
        var byName = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        string? installerName = null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object) continue;
            var name = ReadString(asset, "name");
            var downloadUrl = ReadString(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl)) continue;
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)) continue;

            byName[name] = uri;

            // First installer wins. If a release ever ships multiple installer variants
            // we'll need a runtime-aware picker; for now we only publish x64.
            if (installerName is null &&
                name.EndsWith(".installer.exe", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".installer.exe.sha256", StringComparison.OrdinalIgnoreCase))
            {
                installerName = name;
            }
        }

        if (installerName is null || !byName.TryGetValue(installerName, out var installerUri))
        {
            return (null, null);
        }

        var sidecarName = installerName + ".sha256";
        if (!byName.TryGetValue(sidecarName, out var sidecarUri))
        {
            // Installer exists but its matching sidecar is missing — disable the in-app
            // install path entirely so we never verify against the wrong hash.
            return (null, null);
        }

        return (installerUri, sidecarUri);
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
