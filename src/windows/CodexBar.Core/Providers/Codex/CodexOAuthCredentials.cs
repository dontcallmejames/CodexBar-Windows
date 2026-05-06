using System.Text.Json;

namespace CodexBar.Core.Providers.Codex;

public sealed record CodexOAuthCredentials(string? AccessToken, string? RefreshToken, string? AccountId)
{
    public static async Task<CodexOAuthCredentials> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var tokenRoot = root.TryGetProperty("tokens", out var tokens) ? tokens : root;
        return new CodexOAuthCredentials(
            ReadString(tokenRoot, "access_token"),
            ReadString(tokenRoot, "refresh_token"),
            ReadAccountId(tokenRoot) ?? ReadAccountId(root));
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? ReadAccountId(JsonElement element) =>
        ReadString(element, "account_id") ?? ReadString(element, "accountId");
}
