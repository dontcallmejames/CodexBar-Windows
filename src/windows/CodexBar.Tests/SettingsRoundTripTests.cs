using System;
using System.Linq;
using CodexBar.Core.Models;
using CodexBar.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodexBar.Tests;

[TestClass]
public class SettingsRoundTripTests
{
    // SettingsViewModel lives in the CodexBar.WinUI executable project (TFM
    // net9.0-windows10.0.22621), which this test project (net9.0-windows) cannot
    // reference. So the view-model round-trip is asserted at the AppSettings layer:
    // the new flag defaults true and survives a record mutation, mirroring what
    // SettingsViewModel.ToSettings() carries through.
    [TestMethod]
    public void AntigravityEnabled_DefaultsTrue_AndSurvivesRoundTrip()
    {
        Assert.IsTrue(AppSettings.Default.AntigravityEnabled);

        var toggledOff = AppSettings.Default with { AntigravityEnabled = false };

        Assert.IsFalse(toggledOff.AntigravityEnabled);
    }

    // Guards against the popover/menu enabled-provider list silently dropping a provider —
    // the exact bug where Antigravity was enabled in Settings but never rendered a tab.
    [TestMethod]
    public void EnabledProviders_WhenAllOn_IncludesEveryProvider()
    {
        var allOn = AppSettings.Default with
        {
            CodexEnabled = true,
            ClaudeEnabled = true,
            CursorEnabled = true,
            GeminiEnabled = true,
            CopilotEnabled = true,
            AntigravityEnabled = true,
        };

        CollectionAssert.AreEquivalent(
            Enum.GetValues<UsageProvider>(),
            allOn.EnabledProviders().ToArray());
    }

    [TestMethod]
    public void EnabledProviders_HonorsTheAntigravityToggle()
    {
        Assert.IsTrue(AppSettings.Default.EnabledProviders().Contains(UsageProvider.Antigravity));

        var off = AppSettings.Default with { AntigravityEnabled = false };

        Assert.IsFalse(off.EnabledProviders().Contains(UsageProvider.Antigravity));
    }
}
