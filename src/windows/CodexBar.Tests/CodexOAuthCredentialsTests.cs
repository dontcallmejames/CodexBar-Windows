using CodexBar.Core.Providers.Codex;

namespace CodexBar.Tests;

[TestClass]
public sealed class CodexOAuthCredentialsTests
{
    [TestMethod]
    public async Task ReadsAccessAndRefreshTokens()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
        {
          "tokens": {
            "access_token": "access-123",
            "refresh_token": "refresh-456"
          }
        }
        """);

        var credentials = await CodexOAuthCredentials.ReadAsync(path, CancellationToken.None);

        Assert.AreEqual("access-123", credentials.AccessToken);
        Assert.AreEqual("refresh-456", credentials.RefreshToken);
    }
}
