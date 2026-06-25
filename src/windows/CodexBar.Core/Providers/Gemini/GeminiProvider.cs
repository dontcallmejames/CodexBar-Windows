using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodexBar.Core.Models;
using CodexBar.Core.Paths;
using CodexBar.Core.Refresh;

namespace CodexBar.Core.Providers.Gemini;

public sealed class GeminiProvider : IUsageProvider
{
    private static readonly Uri LoadCodeAssistUri = new("https://cloudcode-pa.googleapis.com/v1internal:loadCodeAssist");
    private static readonly Uri RetrieveUserQuotaUri = new("https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota");
    private static readonly Uri OAuthTokenUri = new("https://oauth2.googleapis.com/token");

    private readonly HttpClient httpClient;
    private readonly IAppPaths paths;
    private readonly IGeminiOAuthClientProvider oauthClientProvider;

    public GeminiProvider(HttpClient httpClient, IAppPaths paths, IGeminiOAuthClientProvider? oauthClientProvider = null)
    {
        this.httpClient = httpClient;
        this.paths = paths;
        this.oauthClientProvider = oauthClientProvider ?? new GeminiOAuthClientProvider();
    }

    public UsageProvider Provider => UsageProvider.Gemini;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        if (await IsUnsupportedAuthModeAsync(cancellationToken))
        {
            return Missing("Gemini CLI OAuth is required for Windows preview usage.");
        }

        if (!File.Exists(paths.GeminiOAuthCredentialsJson))
        {
            return Missing("Gemini CLI OAuth credentials were not found. Run Gemini CLI login first.");
        }

        var credentials = await GeminiCredentials.ReadAsync(paths.GeminiOAuthCredentialsJson, cancellationToken);
        if (string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return Missing("Gemini CLI OAuth access token was not found.");
        }

        if (credentials.IsExpired(DateTimeOffset.Now))
        {
            if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
            {
                return Missing("Gemini CLI OAuth refresh token was not found.");
            }

            try
            {
                credentials = await RefreshAccessTokenAsync(credentials, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return Missing("Gemini CLI OAuth refresh failed. Run Gemini CLI login again, then retry.");
            }
            catch (InvalidOperationException)
            {
                return Missing("Gemini CLI OAuth refresh failed. Run Gemini CLI login again, then retry.");
            }

            if (credentials is null)
            {
                return Missing("Gemini CLI OAuth client metadata was not found. Reinstall or rerun Gemini CLI.");
            }

            await GeminiCredentials.WriteAsync(paths.GeminiOAuthCredentialsJson, credentials, cancellationToken);
        }

        try
        {
            using var load = await PostJsonAsync(
                LoadCodeAssistUri,
                credentials.AccessToken!,
                LoadCodeAssistRequest(null),
                cancellationToken);
            var project = ReadString(load.RootElement, "cloudaicompanionProject")
                ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
                ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT_ID");

            using var quota = await PostJsonAsync(
                RetrieveUserQuotaUri,
                credentials.AccessToken!,
                string.IsNullOrWhiteSpace(project) ? new { } : new { project },
                cancellationToken);

            return GeminiUsageMapper.Map(load.RootElement, quota.RootElement, credentials.Email, DateTimeOffset.Now);
        }
        catch (GeminiRetiredException ex)
        {
            return Missing(ex.Message);
        }
    }

    private async Task<GeminiCredentials?> RefreshAccessTokenAsync(
        GeminiCredentials credentials,
        CancellationToken cancellationToken)
    {
        var client = await oauthClientProvider.ReadClientAsync(cancellationToken);
        if (client is null)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, OAuthTokenUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", "CodexBar-Windows");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = client.Value.ClientId,
            ["client_secret"] = client.Value.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = credentials.RefreshToken!
        });

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var accessToken = ReadString(document.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Gemini OAuth refresh response did not include an access token.");
        }

        return new GeminiCredentials(
            accessToken,
            ReadString(document.RootElement, "refresh_token") ?? credentials.RefreshToken,
            ReadString(document.RootElement, "id_token") ?? credentials.IdToken,
            ReadTokenExpiry(document.RootElement, DateTimeOffset.Now));
    }

    private async Task<JsonDocument> PostJsonAsync(
        Uri uri,
        string accessToken,
        object body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", "CodexBar-Windows");
        request.Content = new StringContent(JsonSerializer.Serialize(body));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GeminiRetiredException(
                "Gemini CLI was retired June 18, 2026. Your Gemini usage now appears under Antigravity. (Paid Gemini Code Assist licenses are unaffected — re-run `gemini` to reconnect.)");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private async Task<bool> IsUnsupportedAuthModeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.GeminiSettingsJson))
        {
            return false;
        }

        await using var stream = File.OpenRead(paths.GeminiSettingsJson);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var authType = ReadString(document.RootElement, "selectedAuthType");
        return authType?.Contains("api-key", StringComparison.OrdinalIgnoreCase) == true ||
            authType?.Contains("vertex-ai", StringComparison.OrdinalIgnoreCase) == true ||
            authType?.Contains("vertex", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static object LoadCodeAssistRequest(string? project) => new
    {
        cloudaicompanionProject = project,
        metadata = new
        {
            ideType = "IDE_UNSPECIFIED",
            platform = "PLATFORM_UNSPECIFIED",
            pluginType = "GEMINI",
            duetProject = project
        },
        mode = "HEALTH_CHECK"
    };

    private static UsageSnapshot Missing(string message) =>
        UsageSnapshot.MissingCredentials(UsageProvider.Gemini, "Gemini", message);

    private static DateTimeOffset? ReadTokenExpiry(JsonElement root, DateTimeOffset now)
    {
        if (root.TryGetProperty("expiry_date", out var expiryDate) &&
            expiryDate.ValueKind == JsonValueKind.Number &&
            expiryDate.TryGetInt64(out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        if (root.TryGetProperty("expires_in", out var expiresIn) &&
            expiresIn.ValueKind == JsonValueKind.Number &&
            expiresIn.TryGetInt64(out var seconds))
        {
            return now.AddSeconds(seconds);
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

internal sealed class GeminiRetiredException(string message) : Exception(message);

public interface IGeminiOAuthClientProvider
{
    Task<(string ClientId, string ClientSecret)?> ReadClientAsync(CancellationToken cancellationToken);
}

public sealed class GeminiOAuthClientProvider : IGeminiOAuthClientProvider
{
    private static readonly Regex ClientIdPattern = new(
        @"OAUTH_CLIENT_ID\s*=\s*['""](?<value>[^'""]+)['""]",
        RegexOptions.Compiled);
    private static readonly Regex ClientSecretPattern = new(
        @"OAUTH_CLIENT_SECRET\s*=\s*['""](?<value>[^'""]+)['""]",
        RegexOptions.Compiled);

    public async Task<(string ClientId, string ClientSecret)?> ReadClientAsync(CancellationToken cancellationToken)
    {
        var environmentClient = ReadEnvironmentClient();
        if (environmentClient is not null)
        {
            return environmentClient;
        }

        return await ReadClientFromRootsAsync(CandidateRoots(), cancellationToken);
    }

    public static async Task<(string ClientId, string ClientSecret)?> ReadClientFromRootsAsync(
        IEnumerable<string> roots,
        CancellationToken cancellationToken)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in CandidateClientFiles(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = await File.ReadAllTextAsync(path, cancellationToken);
                var clientId = MatchValue(ClientIdPattern, text);
                var clientSecret = MatchValue(ClientSecretPattern, text);
                if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                {
                    return (clientId, clientSecret);
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateClientFiles(string root)
    {
        var bundle = Path.Combine(root, "bundle");
        if (Directory.Exists(bundle))
        {
            foreach (var path in Directory.EnumerateFiles(bundle, "*.js", SearchOption.TopDirectoryOnly))
            {
                yield return path;
            }
        }

        foreach (var path in Directory.EnumerateFiles(root, "oauth2.js", SearchOption.AllDirectories))
        {
            yield return path;
        }
    }

    private static (string ClientId, string ClientSecret)? ReadEnvironmentClient()
    {
        var clientId = Environment.GetEnvironmentVariable("GEMINI_OAUTH_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("GEMINI_OAUTH_CLIENT_SECRET");
        return string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)
            ? null
            : (clientId, clientSecret);
    }

    private static IEnumerable<string> CandidateRoots()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "npm", "node_modules", "@google", "gemini-cli");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, "AppData", "Roaming", "npm", "node_modules", "@google", "gemini-cli");
        }
    }

    private static string? MatchValue(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        return match.Success ? match.Groups["value"].Value : null;
    }
}
