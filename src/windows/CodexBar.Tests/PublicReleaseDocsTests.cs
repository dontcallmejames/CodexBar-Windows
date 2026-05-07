namespace CodexBar.Tests;

[TestClass]
public sealed class PublicReleaseDocsTests
{
    [TestMethod]
    public void ReadmePositionsWindowsPreviewAndCreditsOriginalProject()
    {
        var readme = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "README.md")));

        StringAssert.Contains(readme, "CodexBar for Windows");
        StringAssert.Contains(readme, "https://github.com/steipete/CodexBar");
        StringAssert.Contains(readme, "Windows 11");
        StringAssert.Contains(readme, "Cursor");
        StringAssert.Contains(readme, "Gemini");
        StringAssert.Contains(readme, "credentials stay on your machine");
    }

    [TestMethod]
    public void AboutWindowCreditsOriginalProject()
    {
        var about = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "CodexBar.WinApp",
            "Views",
            "AboutWindow.xaml")));

        StringAssert.Contains(about, "Inspired by Peter Steinberger's CodexBar");
    }
}
