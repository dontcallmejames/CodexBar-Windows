using System.Text.Json;

namespace CodexBar.Core.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string path;

    public JsonSettingsStore(string path)
    {
        this.path = path;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return AppSettings.Default;
        }

        await using var stream = File.OpenRead(path);
        var settings = await JsonSerializer.DeserializeAsync<StoredAppSettings>(stream, Options, cancellationToken);
        return settings?.ToAppSettings() ?? AppSettings.Default;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFile = Path.Combine(
            string.IsNullOrEmpty(directory) ? "." : directory,
            $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(path))
            {
                File.Replace(tempFile, path, null);
            }
            else
            {
                File.Move(tempFile, path);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private sealed record StoredAppSettings(
        bool? CodexEnabled,
        bool? ClaudeEnabled,
        bool? CursorEnabled,
        bool? GeminiEnabled,
        bool? MergeTrayIcon,
        bool? ShowUsageAsUsed,
        bool? DockOverviewNearTaskbar,
        bool? LaunchAtStartup,
        int? RefreshMinutes,
        string? CodexSource,
        string? ClaudeSource,
        string? CursorSource,
        string? GeminiSource,
        string? ClaudeManualCookieHeader,
        string? CursorManualCookieHeader)
    {
        public AppSettings ToAppSettings()
        {
            var defaults = AppSettings.Default;

            return new AppSettings(
                CodexEnabled ?? defaults.CodexEnabled,
                ClaudeEnabled ?? defaults.ClaudeEnabled,
                CursorEnabled ?? defaults.CursorEnabled,
                GeminiEnabled ?? defaults.GeminiEnabled,
                MergeTrayIcon ?? defaults.MergeTrayIcon,
                ShowUsageAsUsed ?? defaults.ShowUsageAsUsed,
                DockOverviewNearTaskbar ?? defaults.DockOverviewNearTaskbar,
                LaunchAtStartup ?? defaults.LaunchAtStartup,
                RefreshMinutes is > 0 ? RefreshMinutes.Value : defaults.RefreshMinutes,
                NormalizeSource(CodexSource, defaults.CodexSource),
                NormalizeSource(ClaudeSource, defaults.ClaudeSource),
                NormalizeSource(CursorSource, defaults.CursorSource),
                NormalizeSource(GeminiSource, defaults.GeminiSource),
                ClaudeManualCookieHeader,
                CursorManualCookieHeader);
        }

        private static string NormalizeSource(string? source, string fallback) =>
            string.IsNullOrWhiteSpace(source) ? fallback : source;
    }
}
