using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CodexBar.WinApp;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string? LatestTag,
    Uri? ReleaseUri,
    string StatusText,
    string? ErrorMessage)
{
    public static UpdateCheckResult Available(string latestTag, Uri releaseUri) =>
        new(true, latestTag, releaseUri, $"Update available: {latestTag}", null);

    public static UpdateCheckResult UpToDate(string? latestTag) =>
        new(false, latestTag, null, latestTag is null ? "No release metadata found." : $"Up to date: {latestTag}", null);

    public static UpdateCheckResult Failed(string message) =>
        new(false, null, null, $"Update check failed: {message}", message);
}

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken);
}

public sealed class GitHubUpdateChecker : IUpdateChecker
{
    private static readonly Uri ReleasesApiUri = new("https://api.github.com/repos/dontcallmejames/CodexBar-Windows/releases?per_page=20");
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
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var release = FindLatestRelease(json.RootElement);
            if (release is null)
            {
                return UpdateCheckResult.UpToDate(null);
            }

            var tag = ReadString(release.Value, "tag_name");
            var uriText = ReadString(release.Value, "html_url");
            var releaseUri = string.IsNullOrWhiteSpace(uriText) ? ProviderLinks.ReleasesUri() : new Uri(uriText);

            return versionInfo.IsOlderThan(tag)
                ? UpdateCheckResult.Available(tag!, releaseUri)
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

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
