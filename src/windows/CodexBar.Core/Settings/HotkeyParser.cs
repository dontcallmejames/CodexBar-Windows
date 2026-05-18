using System;
using System.Collections.Generic;

namespace CodexBar.Core.Settings;

/// <summary>
/// Parses human-readable hotkey strings like "Ctrl+Alt+U" into Win32 RegisterHotKey
/// modifier flags + virtual-key code. Format: zero or more modifier tokens separated
/// by '+', followed by exactly one non-modifier key token. Whitespace and case are
/// ignored. Returns false for empty input, missing key, duplicate modifiers, or
/// unknown tokens.
/// </summary>
public static class HotkeyParser
{
    // Match the MOD_* constants from winuser.h so callers can pass the result
    // straight to RegisterHotKey.
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    public readonly record struct ParsedHotkey(uint Modifiers, uint VirtualKey);

    public static bool TryParse(string? input, out ParsedHotkey parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var tokens = input.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;

        uint mods = 0;
        uint? vk = null;

        foreach (var raw in tokens)
        {
            var token = raw.ToUpperInvariant();
            if (TryModifier(token, out var modFlag))
            {
                if ((mods & modFlag) != 0) return false; // duplicate
                mods |= modFlag;
                continue;
            }

            // Non-modifier — must be the (single) key.
            if (vk is not null) return false;
            if (!TryKey(token, out var keyCode)) return false;
            vk = keyCode;
        }

        if (vk is null) return false;
        parsed = new ParsedHotkey(mods, vk.Value);
        return true;
    }

    private static bool TryModifier(string token, out uint flag)
    {
        switch (token)
        {
            case "CTRL":
            case "CONTROL":
                flag = ModControl; return true;
            case "ALT":
            case "MENU":
                flag = ModAlt; return true;
            case "SHIFT":
                flag = ModShift; return true;
            case "WIN":
            case "WINDOWS":
            case "META":
            case "SUPER":
                flag = ModWin; return true;
            default:
                flag = 0; return false;
        }
    }

    private static bool TryKey(string token, out uint vk)
    {
        // Single letter A-Z or digit 0-9 maps directly to its ASCII code (matches VK_A..VK_Z, VK_0..VK_9).
        if (token.Length == 1)
        {
            var c = token[0];
            if (c is >= 'A' and <= 'Z') { vk = c; return true; }
            if (c is >= '0' and <= '9') { vk = c; return true; }
        }

        if (NamedKeys.TryGetValue(token, out var code))
        {
            vk = code;
            return true;
        }

        // F1-F24
        if (token.Length >= 2 && token[0] == 'F' && uint.TryParse(token.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
        {
            vk = 0x6F + fn; // VK_F1 = 0x70 → 0x6F + 1
            return true;
        }

        vk = 0;
        return false;
    }

    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.Ordinal)
    {
        ["SPACE"] = 0x20,
        ["TAB"] = 0x09,
        ["ENTER"] = 0x0D,
        ["RETURN"] = 0x0D,
        ["ESC"] = 0x1B,
        ["ESCAPE"] = 0x1B,
        ["BACKSPACE"] = 0x08,
        ["DELETE"] = 0x2E,
        ["DEL"] = 0x2E,
        ["INSERT"] = 0x2D,
        ["INS"] = 0x2D,
        ["HOME"] = 0x24,
        ["END"] = 0x23,
        ["PAGEUP"] = 0x21,
        ["PGUP"] = 0x21,
        ["PAGEDOWN"] = 0x22,
        ["PGDN"] = 0x22,
        ["LEFT"] = 0x25,
        ["UP"] = 0x26,
        ["RIGHT"] = 0x27,
        ["DOWN"] = 0x28,
        ["PRINTSCREEN"] = 0x2C,
        ["PRTSC"] = 0x2C,
    };
}
