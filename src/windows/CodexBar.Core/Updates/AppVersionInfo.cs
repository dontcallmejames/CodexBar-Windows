using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CodexBar.Core.Updates;

public sealed record AppVersionInfo(string DisplayVersion, string Channel, string CurrentTag)
{
    private static readonly Regex VersionTagPattern = new(
        @"^v(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-preview\.(?<preview>\d+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static AppVersionInfo Current { get; } = FromAssembly();

    public static AppVersionInfo FromMarketingVersion(
        string marketingVersion,
        string? buildNumber,
        string? windowsPreviewNumber = null,
        string? channel = "preview")
    {
        var displayVersion = CleanDisplayVersion(marketingVersion);
        var normalized = NormalizeVersion(displayVersion);

        if (IsStableChannel(channel))
        {
            return new AppVersionInfo(
                displayVersion,
                "stable",
                $"v{normalized}");
        }

        var previewNumber = ResolvePreviewNumber(buildNumber, windowsPreviewNumber);
        return new AppVersionInfo(
            displayVersion,
            "preview",
            $"v{normalized}-preview.{previewNumber}");
    }

    public bool IsOlderThan(string? latestTag)
    {
        if (!TryParseVersionTag(CurrentTag, out var current) ||
            !TryParseVersionTag(latestTag, out var latest))
        {
            return false;
        }

        return current.CompareTo(latest) < 0;
    }

    private static bool IsStableChannel(string? channel) =>
        string.Equals(channel?.Trim(), "stable", StringComparison.OrdinalIgnoreCase);

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
        var channel = metadata.TryGetValue("Channel", out var channelValue) && !string.IsNullOrWhiteSpace(channelValue)
            ? channelValue
            : "preview";
        return FromMarketingVersion(version, buildNumber, windowsPreviewNumber, channel);
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

    private static bool TryParseVersionTag(string? tag, out VersionComparable value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var match = VersionTagPattern.Match(tag.Trim());
        if (!match.Success)
        {
            return false;
        }

        var previewGroup = match.Groups["preview"];
        int? preview = previewGroup.Success
            ? int.Parse(previewGroup.Value, CultureInfo.InvariantCulture)
            : null;

        value = new VersionComparable(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture),
            preview);
        return true;
    }

    private readonly record struct VersionComparable(
        int Major,
        int Minor,
        int Patch,
        int? Preview) : IComparable<VersionComparable>
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
            if (patch != 0)
            {
                return patch;
            }

            // Same major.minor.patch: a stable release (null preview) sorts ABOVE any
            // preview of the same triple (semver prerelease rule: 0.25.0 > 0.25.0-preview.N).
            if (Preview is null && other.Preview is null)
            {
                return 0;
            }

            if (Preview is null)
            {
                return 1;
            }

            if (other.Preview is null)
            {
                return -1;
            }

            return Preview.Value.CompareTo(other.Preview.Value);
        }
    }
}
