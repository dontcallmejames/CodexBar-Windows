using CodexBar.Core.Updates;
using CodexBar.WinApp;

namespace CodexBar.Tests;

[TestClass]
public sealed class AppVersionInfoTests
{
    [TestMethod]
    public void NormalizesMarketingVersionIntoPreviewTag()
    {
        var info = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            windowsPreviewNumber: "3");

        Assert.AreEqual("0.25", info.DisplayVersion);
        Assert.AreEqual("preview", info.Channel);
        Assert.AreEqual("v0.25.0-preview.3", info.CurrentTag);
    }

    [TestMethod]
    public void FallsBackToBuildNumberWhenPreviewNumberIsMissing()
    {
        var current = AppVersionInfo.FromMarketingVersion("0.25", buildNumber: "2");

        Assert.AreEqual("v0.25.0-preview.2", current.CurrentTag);
    }

    [TestMethod]
    public void StripsSourceRevisionMetadataFromInformationalVersion()
    {
        var current = AppVersionInfo.FromMarketingVersion(
            "0.25+309c4c3949b3cdca6797cb3a3db9aca320f25841",
            buildNumber: "60",
            windowsPreviewNumber: "3");

        Assert.AreEqual("0.25", current.DisplayVersion);
        Assert.AreEqual("v0.25.0-preview.3", current.CurrentTag);
    }

    [TestMethod]
    public void ComparesPreviewTagsByPreviewNumber()
    {
        var current = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            windowsPreviewNumber: "2");

        Assert.IsTrue(current.IsOlderThan("v0.25.0-preview.3"));
        Assert.IsFalse(current.IsOlderThan("v0.25.0-preview.2"));
        Assert.IsFalse(current.IsOlderThan("v0.24.0-preview.9"));
    }
}
