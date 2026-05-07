using CodexBar.Core.Providers.Gemini;

namespace CodexBar.Tests;

[TestClass]
public sealed class GeminiCredentialsTests
{
    [TestMethod]
    public async Task ReadsGeminiCliOauthCredentials()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "oauth_creds.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, """
        {
          "access_token": "access",
          "refresh_token": "refresh",
          "id_token": "header.eyJlbWFpbCI6ImdlbWluaUBleGFtcGxlLmNvbSJ9.signature",
          "expiry_date": 1893440000000
        }
        """);

        var credentials = await GeminiCredentials.ReadAsync(path, CancellationToken.None);

        Assert.AreEqual("access", credentials.AccessToken);
        Assert.AreEqual("refresh", credentials.RefreshToken);
        Assert.AreEqual("gemini@example.com", credentials.Email);
        Assert.IsFalse(credentials.IsExpired(DateTimeOffset.FromUnixTimeSeconds(1893430000)));
    }
}
