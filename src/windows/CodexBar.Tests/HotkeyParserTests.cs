using CodexBar.Core.Settings;

namespace CodexBar.Tests;

[TestClass]
public sealed class HotkeyParserTests
{
    [TestMethod]
    public void ParsesCtrlAltU()
    {
        Assert.IsTrue(HotkeyParser.TryParse("Ctrl+Alt+U", out var parsed));
        Assert.AreEqual(HotkeyParser.ModControl | HotkeyParser.ModAlt, parsed.Modifiers);
        Assert.AreEqual((uint)'U', parsed.VirtualKey);
    }

    [TestMethod]
    public void ParsesCtrlShiftF1()
    {
        Assert.IsTrue(HotkeyParser.TryParse("Ctrl+Shift+F1", out var parsed));
        Assert.AreEqual(HotkeyParser.ModControl | HotkeyParser.ModShift, parsed.Modifiers);
        Assert.AreEqual(0x70u, parsed.VirtualKey); // VK_F1
    }

    [TestMethod]
    public void ParsesAltSpace()
    {
        Assert.IsTrue(HotkeyParser.TryParse("Alt+Space", out var parsed));
        Assert.AreEqual(HotkeyParser.ModAlt, parsed.Modifiers);
        Assert.AreEqual(0x20u, parsed.VirtualKey); // VK_SPACE
    }

    [TestMethod]
    public void RejectsEmpty()
    {
        Assert.IsFalse(HotkeyParser.TryParse(string.Empty, out _));
        Assert.IsFalse(HotkeyParser.TryParse(null, out _));
        Assert.IsFalse(HotkeyParser.TryParse("   ", out _));
    }

    [TestMethod]
    public void RejectsGarbage()
    {
        Assert.IsFalse(HotkeyParser.TryParse("Foo+Bar", out _));
        Assert.IsFalse(HotkeyParser.TryParse("Ctrl+Alt", out _));      // no non-modifier key
        Assert.IsFalse(HotkeyParser.TryParse("Ctrl+A+B", out _));      // two non-modifier keys
        Assert.IsFalse(HotkeyParser.TryParse("Ctrl+Ctrl+A", out _));   // duplicate modifier
        Assert.IsFalse(HotkeyParser.TryParse("+", out _));
    }

    [TestMethod]
    public void IsCaseAndWhitespaceInsensitive()
    {
        Assert.IsTrue(HotkeyParser.TryParse(" ctrl + ALT + u ", out var parsed));
        Assert.AreEqual(HotkeyParser.ModControl | HotkeyParser.ModAlt, parsed.Modifiers);
        Assert.AreEqual((uint)'U', parsed.VirtualKey);
    }

    [TestMethod]
    public void ParsesWinKey()
    {
        Assert.IsTrue(HotkeyParser.TryParse("Win+K", out var parsed));
        Assert.AreEqual(HotkeyParser.ModWin, parsed.Modifiers);
        Assert.AreEqual((uint)'K', parsed.VirtualKey);
    }
}
