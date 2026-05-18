using System.Net;
using CodexBar.Core.Updates;

namespace CodexBar.Tests;

[TestClass]
public sealed class UpdateCheckerAssetParsingTests
{
    [TestMethod]
    public async Task ExtractsInstallerAndSha256AssetUrls()
    {
        var json = """
        [
          {
            "tag_name": "v0.25.0-preview.99",
            "html_url": "https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.99",
            "draft": false,
            "assets": [
              { "name": "CodexBar-Windows-0.25-win-x64.zip", "browser_download_url": "https://example.com/portable.zip" },
              { "name": "CodexBar-Windows-0.25-win-x64.installer.exe", "browser_download_url": "https://example.com/installer.exe" },
              { "name": "CodexBar-Windows-0.25-win-x64.installer.exe.sha256", "browser_download_url": "https://example.com/installer.exe.sha256" }
            ]
          }
        ]
        """;

        var result = await CheckAsync(json, "v0.25.0-preview.1");

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual("v0.25.0-preview.99", result.LatestTag);
        Assert.AreEqual(new Uri("https://example.com/installer.exe"), result.InstallerAssetUri);
        Assert.AreEqual(new Uri("https://example.com/installer.exe.sha256"), result.InstallerSha256Uri);
    }

    [TestMethod]
    public async Task ReturnsNullAssetUrisWhenInstallerMissingButStillReportsUpdate()
    {
        var json = """
        [
          {
            "tag_name": "v0.25.0-preview.99",
            "html_url": "https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.99",
            "draft": false,
            "assets": [
              { "name": "CodexBar-Windows-0.25-win-x64.zip", "browser_download_url": "https://example.com/portable.zip" }
            ]
          }
        ]
        """;

        var result = await CheckAsync(json, "v0.25.0-preview.1");

        Assert.IsTrue(result.UpdateAvailable);
        Assert.IsNull(result.InstallerAssetUri);
        Assert.IsNull(result.InstallerSha256Uri);
    }

    [TestMethod]
    public async Task ReturnsNullAssetUrisWhenAssetsArrayAbsent()
    {
        var json = """
        [
          {
            "tag_name": "v0.25.0-preview.99",
            "html_url": "https://github.com/dontcallmejames/CodexBar-Windows/releases/tag/v0.25.0-preview.99",
            "draft": false
          }
        ]
        """;

        var result = await CheckAsync(json, "v0.25.0-preview.1");

        Assert.IsTrue(result.UpdateAvailable);
        Assert.IsNull(result.InstallerAssetUri);
        Assert.IsNull(result.InstallerSha256Uri);
    }

    [TestMethod]
    public async Task SkipsDraftsAndFindsInstallerInPublishedRelease()
    {
        var json = """
        [
          { "tag_name": "v9.9.9-preview.99", "draft": true, "html_url": "https://example.com/draft", "assets": [] },
          {
            "tag_name": "v0.25.0-preview.50",
            "html_url": "https://example.com/published",
            "draft": false,
            "assets": [
              { "name": "x.installer.exe", "browser_download_url": "https://example.com/x.installer.exe" },
              { "name": "x.installer.exe.sha256", "browser_download_url": "https://example.com/x.installer.exe.sha256" }
            ]
          }
        ]
        """;

        var result = await CheckAsync(json, "v0.25.0-preview.1");

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual("v0.25.0-preview.50", result.LatestTag);
        Assert.AreEqual(new Uri("https://example.com/x.installer.exe"), result.InstallerAssetUri);
    }

    private static async Task<UpdateCheckResult> CheckAsync(string releasesJson, string currentTag)
    {
        var version = AppVersionInfo.FromMarketingVersion(
            ParseDisplayVersion(currentTag),
            buildNumber: ParsePreviewNumber(currentTag));
        using var handler = new StaticHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(releasesJson),
        });
        using var client = new HttpClient(handler);
        var checker = new GitHubUpdateChecker(client, version);
        return await checker.CheckAsync(CancellationToken.None);
    }

    private static string ParseDisplayVersion(string tag)
    {
        // tag like "v0.25.0-preview.1" → "0.25"
        var stripped = tag.TrimStart('v');
        var dash = stripped.IndexOf('-');
        var versionPart = dash > 0 ? stripped[..dash] : stripped;
        var parts = versionPart.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : versionPart;
    }

    private static string ParsePreviewNumber(string tag)
    {
        var idx = tag.LastIndexOf('.');
        return idx >= 0 ? tag[(idx + 1)..] : "0";
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage response;
        public StaticHandler(HttpResponseMessage response) { this.response = response; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
