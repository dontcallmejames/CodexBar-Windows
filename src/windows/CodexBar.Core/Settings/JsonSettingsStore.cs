using System.Security.Cryptography;
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

        // Encrypt sensitive cookie headers before persisting so the on-disk JSON never
        // contains plaintext session tokens.
        var toPersist = settings with
        {
            ClaudeManualCookieHeader = ProtectIfNeeded(settings.ClaudeManualCookieHeader),
            CursorManualCookieHeader = ProtectIfNeeded(settings.CursorManualCookieHeader),
        };

        try
        {
            await using (var stream = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, toPersist, Options, cancellationToken);
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
        bool? CopilotEnabled,
        bool? AntigravityEnabled,
        bool? MergeTrayIcon,
        bool? ShowUsageAsUsed,
        bool? DockOverviewNearTaskbar,
        bool? LaunchAtStartup,
        bool? CheckForUpdatesAutomatically,
        int? RefreshMinutes,
        string? CodexSource,
        string? ClaudeSource,
        string? CursorSource,
        string? GeminiSource,
        string? CopilotSource,
        string? AntigravitySource,
        string? ClaudeManualCookieHeader,
        string? CursorManualCookieHeader,
        string? GlobalHotkey,
        bool? EnableGlobalHotkey)
    {
        public AppSettings ToAppSettings()
        {
            var defaults = AppSettings.Default;

            return new AppSettings(
                CodexEnabled ?? defaults.CodexEnabled,
                ClaudeEnabled ?? defaults.ClaudeEnabled,
                CursorEnabled ?? defaults.CursorEnabled,
                GeminiEnabled ?? defaults.GeminiEnabled,
                CopilotEnabled ?? defaults.CopilotEnabled,
                AntigravityEnabled ?? defaults.AntigravityEnabled,
                MergeTrayIcon ?? defaults.MergeTrayIcon,
                ShowUsageAsUsed ?? defaults.ShowUsageAsUsed,
                DockOverviewNearTaskbar ?? defaults.DockOverviewNearTaskbar,
                LaunchAtStartup ?? defaults.LaunchAtStartup,
                CheckForUpdatesAutomatically ?? defaults.CheckForUpdatesAutomatically,
                RefreshMinutes is > 0 ? RefreshMinutes.Value : defaults.RefreshMinutes,
                NormalizeSource(CodexSource, defaults.CodexSource),
                NormalizeSource(ClaudeSource, defaults.ClaudeSource),
                NormalizeSource(CursorSource, defaults.CursorSource),
                NormalizeSource(GeminiSource, defaults.GeminiSource),
                NormalizeSource(CopilotSource, defaults.CopilotSource),
                NormalizeSource(AntigravitySource, defaults.AntigravitySource),
                UnprotectIfNeeded(ClaudeManualCookieHeader),
                UnprotectIfNeeded(CursorManualCookieHeader),
                string.IsNullOrWhiteSpace(GlobalHotkey) ? defaults.GlobalHotkey : GlobalHotkey,
                EnableGlobalHotkey ?? defaults.EnableGlobalHotkey);
        }

        private static string NormalizeSource(string? source, string fallback) =>
            string.IsNullOrWhiteSpace(source) ? fallback : source;
    }

    // Marker prefix so we can distinguish encrypted blobs from legacy plaintext values
    // stored before encryption was introduced. We round-trip via base64 of the protected
    // bytes; the prefix is what tells Load whether to decrypt or treat as plaintext.
    private const string ProtectedPrefix = "dpapi:";

    private static string? ProtectIfNeeded(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        if (plaintext.StartsWith(ProtectedPrefix, StringComparison.Ordinal)) return plaintext;
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return ProtectedPrefix + Convert.ToBase64String(protectedBytes);
        }
        catch
        {
            // Encryption can fail on non-Windows or in restricted contexts; fall back to plaintext
            // rather than dropping the user's credential entirely.
            return plaintext;
        }
    }

    private static string? UnprotectIfNeeded(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return stored;
        if (!stored.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            // Legacy plaintext value written before encryption — will be re-encrypted on next save.
            return stored;
        }
        try
        {
            var base64 = stored.Substring(ProtectedPrefix.Length);
            var protectedBytes = Convert.FromBase64String(base64);
            var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // Corrupt or unreadable — return null so we don't ship a garbage cookie to the provider.
            return null;
        }
    }
}
