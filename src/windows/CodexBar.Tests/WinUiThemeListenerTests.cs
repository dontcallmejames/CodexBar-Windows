using CodexBar.WinUI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class WinUiThemeListenerTests
{
    [TestMethod]
    public void System_ReturnsProbeResult_Dark()
    {
        var listener = new ThemeListener(() => CodexBarTheme.Dark);
        listener.UserPreference = ThemePreference.System;
        Assert.AreEqual(CodexBarTheme.Dark, listener.Effective);
    }

    [TestMethod]
    public void System_ReturnsProbeResult_Light()
    {
        var listener = new ThemeListener(() => CodexBarTheme.Light);
        listener.UserPreference = ThemePreference.System;
        Assert.AreEqual(CodexBarTheme.Light, listener.Effective);
    }

    [TestMethod]
    public void UserOverride_Light_IgnoresSystemDark()
    {
        var listener = new ThemeListener(() => CodexBarTheme.Dark);
        listener.UserPreference = ThemePreference.Light;
        Assert.AreEqual(CodexBarTheme.Light, listener.Effective);
    }

    [TestMethod]
    public void UserOverride_Dark_IgnoresSystemLight()
    {
        var listener = new ThemeListener(() => CodexBarTheme.Light);
        listener.UserPreference = ThemePreference.Dark;
        Assert.AreEqual(CodexBarTheme.Dark, listener.Effective);
    }

    [TestMethod]
    public void UserPreferenceChange_FiresChangedEvent()
    {
        var listener = new ThemeListener(() => CodexBarTheme.Light);
        int firedCount = 0;
        listener.Changed += (_, _) => firedCount++;
        listener.UserPreference = ThemePreference.Dark;
        Assert.AreEqual(1, firedCount);
    }

    [TestMethod]
    public void Refresh_FiresChangedEvent()
    {
        var listener = new ThemeListener(() => CodexBarTheme.Light);
        int firedCount = 0;
        listener.Changed += (_, _) => firedCount++;
        listener.Refresh();
        Assert.AreEqual(1, firedCount);
    }
}
