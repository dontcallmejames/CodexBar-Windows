using System.Net;
using System.Security.Cryptography;
using System.Text;
using CodexBar.Core.Updates;

namespace CodexBar.Tests;

[TestClass]
public sealed class UpdateInstallerTests
{
    private static readonly Uri InstallerUri = new("https://example.com/CodexBar.installer.exe");
    private static readonly Uri Sha256Uri = new("https://example.com/CodexBar.installer.exe.sha256");

    [TestMethod]
    public async Task DownloadsAndVerifiesMatchingSha256()
    {
        var payload = Encoding.UTF8.GetBytes("pretend-installer-bytes");
        var hex = Convert.ToHexString(SHA256.HashData(payload));
        var sidecar = $"{hex}  CodexBar.installer.exe\n";

        using var handler = new RouteHandler();
        handler.Routes[InstallerUri] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        handler.Routes[Sha256Uri] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sidecar) };

        using var client = new HttpClient(handler);
        var installer = new UpdateInstaller(client, AppVersionInfo.FromMarketingVersion("0.25", "1"));

        var result = await installer.PrepareAsync(InstallerUri, Sha256Uri, progress: null, CancellationToken.None);

        try
        {
            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.IsNotNull(result.LocalInstallerPath);
            Assert.IsTrue(File.Exists(result.LocalInstallerPath));
            CollectionAssert.AreEqual(payload, File.ReadAllBytes(result.LocalInstallerPath!));
        }
        finally
        {
            if (result.LocalInstallerPath is not null && File.Exists(result.LocalInstallerPath))
            {
                File.Delete(result.LocalInstallerPath);
            }
        }
    }

    [TestMethod]
    public async Task FailsWhenSha256Mismatches()
    {
        var payload = Encoding.UTF8.GetBytes("pretend-installer-bytes");
        var wrongHex = new string('0', 64);
        var sidecar = $"{wrongHex}  CodexBar.installer.exe\n";

        using var handler = new RouteHandler();
        handler.Routes[InstallerUri] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        handler.Routes[Sha256Uri] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sidecar) };

        using var client = new HttpClient(handler);
        var installer = new UpdateInstaller(client, AppVersionInfo.FromMarketingVersion("0.25", "1"));

        var result = await installer.PrepareAsync(InstallerUri, Sha256Uri, progress: null, CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.LocalInstallerPath);
        StringAssert.Contains(result.ErrorMessage ?? "", "SHA-256");
    }

    [TestMethod]
    public async Task FailsWhenInstallerDownloadReturns404()
    {
        using var handler = new RouteHandler();
        handler.Routes[InstallerUri] = () => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("nope") };
        handler.Routes[Sha256Uri] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("deadbeef  x") };

        using var client = new HttpClient(handler);
        var installer = new UpdateInstaller(client, AppVersionInfo.FromMarketingVersion("0.25", "1"));

        var result = await installer.PrepareAsync(InstallerUri, Sha256Uri, progress: null, CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.LocalInstallerPath);
        Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    public async Task ReportsProgressOverTheCourseOfTheDownload()
    {
        var payload = new byte[256 * 1024]; // 256 KiB so we get multiple read chunks (buffer is 80 KiB)
        new Random(42).NextBytes(payload);
        var hex = Convert.ToHexString(SHA256.HashData(payload));

        using var handler = new RouteHandler();
        handler.Routes[InstallerUri] = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload),
        };
        handler.Routes[Sha256Uri] = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{hex}  payload"),
        };

        using var client = new HttpClient(handler);
        var installer = new UpdateInstaller(client, AppVersionInfo.FromMarketingVersion("0.25", "1"));
        var reported = new List<double>();
        var progress = new Progress<double>(value =>
        {
            lock (reported) reported.Add(value);
        });

        var result = await installer.PrepareAsync(InstallerUri, Sha256Uri, progress, CancellationToken.None);

        try
        {
            Assert.IsTrue(result.Success, result.ErrorMessage);
            // Progress reporter is async, so settle before asserting.
            await Task.Delay(50);
            lock (reported)
            {
                Assert.IsTrue(reported.Count > 0);
                // Progress<T> posts via ThreadPool when no SynchronizationContext is captured,
                // so reports can arrive out of order. Assert the MAX hit 1.0 rather than the tail.
                double max = 0;
                foreach (var p in reported)
                {
                    Assert.IsTrue(p is >= 0.0 and <= 1.0);
                    if (p > max) max = p;
                }
                Assert.IsTrue(max >= 0.99, $"max progress was {max}");
            }
        }
        finally
        {
            if (result.LocalInstallerPath is not null && File.Exists(result.LocalInstallerPath))
            {
                File.Delete(result.LocalInstallerPath);
            }
        }
    }

    private sealed class RouteHandler : HttpMessageHandler
    {
        public Dictionary<Uri, Func<HttpResponseMessage>> Routes { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null || !Routes.TryGetValue(request.RequestUri, out var factory))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
            return Task.FromResult(factory());
        }
    }
}
