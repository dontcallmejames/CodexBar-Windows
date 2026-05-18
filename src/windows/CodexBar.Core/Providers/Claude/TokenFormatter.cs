using System.Globalization;

namespace CodexBar.Core.Providers.Claude;

/// <summary>
/// Abbreviates token counts for compact UI display, e.g. 1,500,000 → "1.5M".
/// </summary>
public static class TokenFormatter
{
    public static string Format(long tokens)
    {
        if (tokens < 0)
        {
            return "0";
        }

        if (tokens >= 1_000_000)
        {
            return Format(tokens / 1_000_000d, "M");
        }

        if (tokens >= 1_000)
        {
            return Format(tokens / 1_000d, "K");
        }

        return tokens.ToString(CultureInfo.InvariantCulture);
    }

    private static string Format(double value, string suffix)
    {
        // Drop decimals at >= 100 (e.g. 847.3K reads as noise; "847K" is clearer).
        if (value >= 100d)
        {
            return ((long)Math.Floor(value)).ToString(CultureInfo.InvariantCulture) + suffix;
        }

        // Otherwise show one decimal, trimming trailing ".0" for whole numbers.
        var rounded = Math.Floor(value * 10) / 10d;
        if (rounded == Math.Floor(rounded))
        {
            return ((long)rounded).ToString(CultureInfo.InvariantCulture) + suffix;
        }

        return rounded.ToString("0.0", CultureInfo.InvariantCulture) + suffix;
    }
}
