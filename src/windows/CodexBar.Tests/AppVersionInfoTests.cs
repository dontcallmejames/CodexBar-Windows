using CodexBar.Core.Updates;

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

    [TestMethod]
    public void StableChannelProducesStableTagWithoutPreviewSuffix()
    {
        var info = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            windowsPreviewNumber: "13",
            channel: "stable");

        Assert.AreEqual("0.25", info.DisplayVersion);
        Assert.AreEqual("stable", info.Channel);
        Assert.AreEqual("v0.25.0", info.CurrentTag);
    }

    [TestMethod]
    public void PreviewChannelProducesPreviewTag()
    {
        var info = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            windowsPreviewNumber: "13",
            channel: "preview");

        Assert.AreEqual("preview", info.Channel);
        Assert.AreEqual("v0.25.0-preview.13", info.CurrentTag);
    }

    [TestMethod]
    public void PreviewIsOlderThanStableOfSameTriple()
    {
        var preview = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            windowsPreviewNumber: "15",
            channel: "preview");

        Assert.IsTrue(preview.IsOlderThan("v0.25.0"));
    }

    [TestMethod]
    public void StableIsNotOlderThanOlderPreviewOfSameTriple()
    {
        var stable = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            channel: "stable");

        Assert.IsFalse(stable.IsOlderThan("v0.25.0-preview.15"));
    }

    [TestMethod]
    public void StableIsOlderThanHigherStable()
    {
        var stable = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            channel: "stable");

        Assert.IsTrue(stable.IsOlderThan("v0.25.1"));
        Assert.IsTrue(stable.IsOlderThan("v0.26.0"));
        Assert.IsFalse(stable.IsOlderThan("v0.25.0"));
    }

    [TestMethod]
    public void ParsesStableTagAndComparesAcrossChannelBoundary()
    {
        var stable = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            channel: "stable");
        var preview = AppVersionInfo.FromMarketingVersion(
            "0.25",
            buildNumber: "60",
            windowsPreviewNumber: "14",
            channel: "preview");

        // Stable tag parses (otherwise IsOlderThan would return false for a higher stable).
        Assert.IsTrue(stable.IsOlderThan("v0.26.0"));

        // Across the stable/preview boundary, both directions.
        Assert.IsFalse(stable.IsOlderThan("v0.25.0-preview.14"));
        Assert.IsTrue(preview.IsOlderThan("v0.25.0"));
    }
}
