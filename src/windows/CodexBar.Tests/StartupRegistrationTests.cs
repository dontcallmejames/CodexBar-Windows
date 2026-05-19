using CodexBar.Core.Startup;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace CodexBar.Tests;

[TestClass]
public sealed class StartupRegistrationTests
{
    // Each test runs against a fresh per-test subtree under HKCU\Software\CodexBar.Tests so
    // we never touch the user's real Run key. The StartupRegistration class is constructed
    // with this scoped root and the rest of its behaviour is exercised against the real
    // registry — keeps the test fast and means we're testing the actual production code
    // path, not a mock.
    private const string TestRoot = @"Software\CodexBar.Tests\StartupRegistrationTests";

    private RegistryKey scopedRoot = null!;
    private StartupRegistration registration = null!;

    [TestInitialize]
    public void Setup()
    {
        // Clean any leftover state from a prior crashed run.
        Registry.CurrentUser.DeleteSubKeyTree(TestRoot, throwOnMissingSubKey: false);
        scopedRoot = Registry.CurrentUser.CreateSubKey(TestRoot, writable: true)!;
        registration = new StartupRegistration(scopedRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        scopedRoot?.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(TestRoot, throwOnMissingSubKey: false);
    }

    [TestMethod]
    public void IsEnabledReturnsFalseWhenValueAbsent()
    {
        Assert.IsFalse(registration.IsEnabled());
    }

    [TestMethod]
    public void SetEnabledTrueWritesQuotedExePath()
    {
        registration.SetEnabled(true, @"C:\Program Files\CodexBar\CodexBar.WinUI.exe");

        using var runKey = scopedRoot.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
        Assert.IsNotNull(runKey);
        var value = runKey!.GetValue(StartupRegistration.ValueName) as string;
        // Quoting matters — installer paths contain spaces and Windows shell parses the
        // Run value as a command line.
        Assert.AreEqual("\"C:\\Program Files\\CodexBar\\CodexBar.WinUI.exe\"", value);
        Assert.IsTrue(registration.IsEnabled());
    }

    [TestMethod]
    public void SetEnabledFalseRemovesValue()
    {
        registration.SetEnabled(true, @"C:\app\CodexBar.WinUI.exe");
        Assert.IsTrue(registration.IsEnabled());

        registration.SetEnabled(false, @"C:\app\CodexBar.WinUI.exe");

        Assert.IsFalse(registration.IsEnabled());
    }

    [TestMethod]
    public void SetEnabledFalseIsIdempotentWhenValueAbsent()
    {
        // Should not throw even if the value never existed.
        registration.SetEnabled(false, @"C:\app\CodexBar.WinUI.exe");
        Assert.IsFalse(registration.IsEnabled());
    }
}
