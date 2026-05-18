using CodexBar.Core.Models;
using CodexBar.Core.Updates;
using CodexBar.Core.Settings;
using System.Reflection;

namespace CodexBar.Core;

public static class BugReportBuilder
{
    public static string BuildDiagnosticSummary(
        AppSettings settings,
        IReadOnlyList<UsageSnapshot> snapshots,
        string? appVersion = null,
        string? osDescription = null,
        UpdateCheckResult? updateStatus = null)
    {
        appVersion ??= Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        osDescription ??= System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var byProvider = snapshots.ToDictionary(snapshot => snapshot.Provider);

        return string.Join(
            Environment.NewLine,
            "CodexBar diagnostic summary",
            $"App version: {appVersion}",
            $"OS: {osDescription}",
            $"Update status: {FormatUpdateStatus(updateStatus)}",
            $"Taskbar dock enabled: {settings.DockOverviewNearTaskbar}",
            $"Show usage as used: {settings.ShowUsageAsUsed}",
            "Providers:",
            ProviderLine(UsageProvider.Codex, settings.CodexEnabled, byProvider),
            ProviderLine(UsageProvider.Claude, settings.ClaudeEnabled, byProvider),
            ProviderLine(UsageProvider.Cursor, settings.CursorEnabled, byProvider),
            ProviderLine(UsageProvider.Gemini, settings.GeminiEnabled, byProvider),
            ProviderLine(UsageProvider.Copilot, settings.CopilotEnabled, byProvider),
            string.Empty,
            "No tokens, cookies, OAuth files, or credential contents are included.");
    }

    private static string ProviderLine(
        UsageProvider provider,
        bool enabled,
        IReadOnlyDictionary<UsageProvider, UsageSnapshot> snapshots)
    {
        if (!enabled)
        {
            return $"- {DisplayName(provider)}: disabled";
        }

        if (!snapshots.TryGetValue(provider, out var snapshot))
        {
            return $"- {DisplayName(provider)}: enabled, no snapshot yet";
        }

        var state = snapshot.IsStale ? "stale" : "fresh";
        var windows = snapshot.Windows.Count == 1 ? "1 usage window" : $"{snapshot.Windows.Count} usage windows";
        var error = string.IsNullOrWhiteSpace(snapshot.ErrorMessage)
            ? null
            : $", latest error: {SanitizeSingleLine(snapshot.ErrorMessage)}";
        return $"- {DisplayName(provider)}: enabled, {state}, {windows}{error}";
    }

    private static string DisplayName(UsageProvider provider) =>
        provider switch
        {
            UsageProvider.Codex => "Codex",
            UsageProvider.Claude => "Claude",
            UsageProvider.Cursor => "Cursor",
            UsageProvider.Gemini => "Gemini",
            _ => provider.ToString()
        };

    private static string SanitizeSingleLine(string value) =>
        value.ReplaceLineEndings(" ").Trim();

    private static string FormatUpdateStatus(UpdateCheckResult? updateStatus)
    {
        if (updateStatus is null)
        {
            return "not checked";
        }

        if (updateStatus.UpdateAvailable)
        {
            return $"update available ({updateStatus.LatestTag})";
        }

        return updateStatus.StatusText;
    }
}
