using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodexBar.Core.Providers.Gemini;

public sealed record GeminiCredentials(
    string? AccessToken,
    string? RefreshToken,
    string? IdToken,
    DateTimeOffset? ExpiresAt)
{
    public string? Email => JwtEmail(IdToken);

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now.AddMinutes(1);

    public static async Task<GeminiCredentials> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        return new GeminiCredentials(
            Clean(ReadString(root, "access_token")),
            Clean(ReadString(root, "refresh_token")),
            Clean(ReadString(root, "id_token")),
            ReadExpiresAt(root));
    }

    public static async Task WriteAsync(
        string path,
        GeminiCredentials credentials,
        CancellationToken cancellationToken)
    {
        var root = File.Exists(path)
            ? JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken))?.AsObject() ?? new JsonObject()
            : new JsonObject();

        root["access_token"] = credentials.AccessToken;
        if (!string.IsNullOrWhiteSpace(credentials.RefreshToken))
        {
            root["refresh_token"] = credentials.RefreshToken;
        }

        if (!string.IsNullOrWhiteSpace(credentials.IdToken))
        {
            root["id_token"] = credentials.IdToken;
        }

        if (credentials.ExpiresAt is not null)
        {
            root["expiry_date"] = credentials.ExpiresAt.Value.ToUnixTimeMilliseconds();
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            path,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private static DateTimeOffset? ReadExpiresAt(JsonElement root)
    {
        if (root.TryGetProperty("expiry_date", out var expiryDate) &&
            expiryDate.ValueKind == JsonValueKind.Number &&
            expiryDate.TryGetInt64(out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        if (root.TryGetProperty("expires_at", out var expiresAt) &&
            expiresAt.ValueKind == JsonValueKind.Number &&
            expiresAt.TryGetInt64(out var seconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? JwtEmail(string? idToken)
    {
        var parts = idToken?.Split('.');
        if (parts is not { Length: >= 2 })
        {
            return null;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var document = JsonDocument.Parse(payload);
            return ReadString(document.RootElement, "email");
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
