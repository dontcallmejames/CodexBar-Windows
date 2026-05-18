using System.Linq;
using CodexBar.Core.Providers.Copilot;

namespace CodexBar.Tests;

[TestClass]
public sealed class CopilotApiHeadersTests
{
    [TestMethod]
    public void BuildRequestEmitsAllRequiredHeaders()
    {
        using var request = CopilotProvider.BuildRequest("ghp_fake_token");

        // GitHub returns 403 without the editor-spoofing combo — assert each header lands.
        Assert.AreEqual("token", request.Headers.Authorization?.Scheme);
        Assert.AreEqual("ghp_fake_token", request.Headers.Authorization?.Parameter);

        Assert.IsTrue(request.Headers.Accept.Any(h => h.MediaType == "application/json"));

        Assert.AreEqual(CopilotProvider.EditorVersion, GetFirst(request, "Editor-Version"));
        Assert.AreEqual(CopilotProvider.EditorPluginVersion, GetFirst(request, "Editor-Plugin-Version"));
        Assert.AreEqual(CopilotProvider.UserAgent, GetFirst(request, "User-Agent"));
        Assert.AreEqual(CopilotProvider.GithubApiVersion, GetFirst(request, "X-Github-Api-Version"));

        Assert.AreEqual(CopilotProvider.UsageUri, request.RequestUri);
    }

    private static string? GetFirst(System.Net.Http.HttpRequestMessage req, string name)
    {
        if (req.Headers.TryGetValues(name, out var values))
        {
            return values.FirstOrDefault();
        }
        return null;
    }
}
