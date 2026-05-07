using CodexBar.WinApp;

namespace CodexBar.Tests;

[TestClass]
public sealed class StartupRegistrationTests
{
    [TestMethod]
    public void BuildsQuotedRunValueForExecutablePath()
    {
        var value = StartupRegistration.BuildRunValue(@"C:\Program Files\CodexBar\CodexBar.WinApp.exe");

        Assert.AreEqual(@"""C:\Program Files\CodexBar\CodexBar.WinApp.exe""", value);
    }
}
