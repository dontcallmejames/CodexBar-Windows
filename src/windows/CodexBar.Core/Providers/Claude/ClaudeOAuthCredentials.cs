using System.Text.Json;

namespace CodexBar.Core.Providers.Claude;

public sealed record ClaudeOAuthCredentials(
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<string> Scopes,
    string? RateLimitTier,
    string? SubscriptionType)
{
    public static async Task<ClaudeOAuthCredentials> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (!root.TryGetProperty("claudeAiOauth", out var oauth) || oauth.ValueKind != JsonValueKind.Object)
        {
            return Empty;
        }

        return new ClaudeOAuthCredentials(
            Clean(ReadString(oauth, "accessToken")),
            Clean(ReadString(oauth, "refreshToken")),
            ReadExpiresAt(oauth),
            ReadScopes(oauth),
            Clean(ReadString(oauth, "rateLimitTier")),
            Clean(ReadString(oauth, "subscriptionType")));
    }

    public static ClaudeOAuthCredentials Empty { get; } = new(null, null, null, Array.Empty<string>(), null, null);

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static DateTimeOffset? ReadExpiresAt(JsonElement element)
    {
        if (!element.TryGetProperty("expiresAt", out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetInt64(out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : null;
    }

    private static IReadOnlyList<string> ReadScopes(JsonElement element)
    {
        if (!element.TryGetProperty("scopes", out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => Clean(item.GetString()))
            .OfType<string>()
            .ToArray();
    }
}
