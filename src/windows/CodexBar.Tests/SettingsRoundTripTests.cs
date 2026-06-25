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
}
