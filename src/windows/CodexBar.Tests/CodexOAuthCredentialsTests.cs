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

    [TestMethod]
    [DataRow("tokens", "account_id", "token-account")]
    [DataRow("tokens", "accountId", "token-account")]
    [DataRow("root", "account_id", "root-account")]
    [DataRow("root", "accountId", "root-account")]
    public async Task ReadsAccountIdFromTokensOrRoot(string location, string propertyName, string expectedAccountId)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var rootAccount = location == "root" ? $"""
          "{propertyName}": "root-account",
        """ : string.Empty;
        var tokenAccount = location == "tokens" ? $"""
            "{propertyName}": "token-account",
        """ : string.Empty;
        await File.WriteAllTextAsync(path, $$"""
        {
        {{rootAccount}}
          "tokens": {
            "access_token": "access-123",
            "refresh_token": "refresh-456",
        {{tokenAccount}}
            "id_token": "id-000"
          }
        }
        """);

        var credentials = await CodexOAuthCredentials.ReadAsync(path, CancellationToken.None);

        Assert.AreEqual(expectedAccountId, credentials.AccountId);
    }

    [TestMethod]
    public async Task TokenAccountIdTakesPriorityOverRootAccountId()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "auth.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
        {
          "accountId": "root-account",
          "tokens": {
            "access_token": "access-123",
            "refresh_token": "refresh-456",
            "account_id": "token-account"
          }
        }
        """);

        var credentials = await CodexOAuthCredentials.ReadAsync(path, CancellationToken.None);

        Assert.AreEqual("token-account", credentials.AccountId);
    }
}
