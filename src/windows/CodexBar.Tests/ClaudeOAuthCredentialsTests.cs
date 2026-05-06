using CodexBar.Core.Providers.Claude;

namespace CodexBar.Tests;

[TestClass]
public sealed class ClaudeOAuthCredentialsTests
{
    [TestMethod]
    public async Task ReadsClaudeAiOauthTokensAndScopes()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".credentials.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
        {
          "claudeAiOauth": {
            "accessToken": "access-123",
            "refreshToken": "refresh-456",
            "expiresAt": 1893456000000,
            "scopes": ["user:profile", "user:inference"],
            "rateLimitTier": "claude_max",
            "subscriptionType": "Max"
          }
        }
        """);

        var credentials = await ClaudeOAuthCredentials.ReadAsync(path, CancellationToken.None);

        Assert.AreEqual("access-123", credentials.AccessToken);
        Assert.AreEqual("refresh-456", credentials.RefreshToken);
        CollectionAssert.AreEqual(new[] { "user:profile", "user:inference" }, credentials.Scopes.ToArray());
        Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1893456000000), credentials.ExpiresAt);
        Assert.AreEqual("claude_max", credentials.RateLimitTier);
        Assert.AreEqual("Max", credentials.SubscriptionType);
    }

    [TestMethod]
    public async Task MissingClaudeAiOauthReturnsEmptyCredentials()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".credentials.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{}");

        var credentials = await ClaudeOAuthCredentials.ReadAsync(path, CancellationToken.None);

        Assert.IsNull(credentials.AccessToken);
        Assert.IsNull(credentials.RefreshToken);
        Assert.AreEqual(0, credentials.Scopes.Count);
    }
}
