using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CodexBar.WinApp;

public sealed record AppVersionInfo(string DisplayVersion, string Channel, string CurrentTag)
{
    private static readonly Regex PreviewTagPattern = new(
        @"^v(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)-preview\.(?<preview>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static AppVersionInfo Current { get; } = FromAssembly();

    public static AppVersionInfo FromMarketingVersion(
        string marketingVersion,
        string? buildNumber,
        string? windowsPreviewNumber = null)
    {
        var displayVersion = CleanDisplayVersion(marketingVersion);
        var normalized = NormalizeVersion(displayVersion);
        var previewNumber = ResolvePreviewNumber(buildNumber, windowsPreviewNumber);
        return new AppVersionInfo(
            displayVersion,
            "preview",
            $"v{normalized}-preview.{previewNumber}");
    }

    public bool IsOlderThan(string? latestTag)
    {
        if (!TryParsePreviewTag(CurrentTag, out var current) ||
            !TryParsePreviewTag(latestTag, out var latest))
        {
            return false;
        }

        return current.CompareTo(latest) < 0;
    }

    private static AppVersionInfo FromAssembly()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            assembly.GetName().Version?.ToString(3) ??
            "0.0";
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        metadata.TryGetValue("BuildNumber", out var buildNumber);
        metadata.TryGetValue("WindowsPreviewNumber", out var windowsPreviewNumber);
        return FromMarketingVersion(version, buildNumber, windowsPreviewNumber);
    }

    private static string ResolvePreviewNumber(string? buildNumber, string? windowsPreviewNumber)
    {
        if (!string.IsNullOrWhiteSpace(windowsPreviewNumber))
        {
            return windowsPreviewNumber.Trim();
        }

        return string.IsNullOrWhiteSpace(buildNumber) ? "0" : buildNumber.Trim();
    }

    private static string CleanDisplayVersion(string version)
    {
        var cleanVersion = version.Split('+', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(cleanVersion) ? "0.0" : cleanVersion;
    }

    private static string NormalizeVersion(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 3)
        {
            return string.Join(".", parts.Take(3));
        }

        if (parts.Length == 2)
        {
            return $"{parts[0]}.{parts[1]}.0";
        }

        return parts.Length == 1 ? $"{parts[0]}.0.0" : "0.0.0";
    }

    private static bool TryParsePreviewTag(string? tag, out VersionComparable value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var match = PreviewTagPattern.Match(tag.Trim());
        if (!match.Success)
        {
            return false;
        }

        value = new VersionComparable(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["preview"].Value, CultureInfo.InvariantCulture));
        return true;
    }

    private readonly record struct VersionComparable(
        int Major,
        int Minor,
        int Patch,
        int Preview) : IComparable<VersionComparable>
    {
        public int CompareTo(VersionComparable other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0)
            {
                return major;
            }

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0)
            {
                return minor;
            }

            var patch = Patch.CompareTo(other.Patch);
            return patch != 0 ? patch : Preview.CompareTo(other.Preview);
        }
    }
}
