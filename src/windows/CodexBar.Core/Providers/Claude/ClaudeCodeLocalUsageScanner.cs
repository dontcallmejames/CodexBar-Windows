using System.Text.Json;

namespace CodexBar.Core.Providers.Claude;

public sealed record ClaudeCodeUsageReport(
    long TodayInputTokens,
    long TodayOutputTokens,
    long TodayCacheCreationTokens,
    long TodayCacheReadTokens,
    long Last7DaysInputTokens,
    long Last7DaysOutputTokens,
    int SessionFilesScanned)
{
    public long TodayTotalTokens =>
        TodayInputTokens + TodayOutputTokens + TodayCacheCreationTokens + TodayCacheReadTokens;

    public long Last7DaysTotalTokens => Last7DaysInputTokens + Last7DaysOutputTokens;

    public static ClaudeCodeUsageReport Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Scans Claude Code's local JSONL session files (under
/// <c>%USERPROFILE%\.claude\projects\</c>) and aggregates per-day token spend in
/// the style of the ccusage CLI. All work is purely local — no network calls.
/// </summary>
public sealed class ClaudeCodeLocalUsageScanner
{
    // Files older than this are skipped without being opened.
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(8);

    private readonly string projectsRoot;

    public ClaudeCodeLocalUsageScanner(string? claudeProjectsRoot = null)
    {
        projectsRoot = string.IsNullOrWhiteSpace(claudeProjectsRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects")
            : claudeProjectsRoot;
    }

    public ClaudeCodeUsageReport Scan(DateTimeOffset asOfLocal, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(projectsRoot))
        {
            return ClaudeCodeUsageReport.Empty;
        }

        var today = asOfLocal.LocalDateTime.Date;
        var sevenDaysAgo = today.AddDays(-6); // inclusive 7-day window ending today
        var staleCutoffUtc = asOfLocal.UtcDateTime - MaxAge;

        long todayInput = 0, todayOutput = 0, todayCacheCreate = 0, todayCacheRead = 0;
        long weekInput = 0, weekOutput = 0;
        var seenMessageIds = new HashSet<string>(StringComparer.Ordinal);
        var filesScanned = 0;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(projectsRoot, "*.jsonl", SearchOption.AllDirectories);
        }
        catch (Exception)
        {
            return ClaudeCodeUsageReport.Empty;
        }

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(file);
                if (!info.Exists || info.LastWriteTimeUtc < staleCutoffUtc)
                {
                    continue;
                }
            }
            catch (Exception)
            {
                continue;
            }

            filesScanned++;
            try
            {
                using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (line.Length == 0)
                    {
                        continue;
                    }

                    if (!TryParseLine(line, out var entry))
                    {
                        continue;
                    }

                    if (entry.MessageId is { Length: > 0 } id && !seenMessageIds.Add(id))
                    {
                        continue;
                    }

                    var localDate = entry.Timestamp.LocalDateTime.Date;
                    if (localDate < sevenDaysAgo || localDate > today)
                    {
                        continue;
                    }

                    if (localDate == today)
                    {
                        todayInput += entry.InputTokens;
                        todayOutput += entry.OutputTokens;
                        todayCacheCreate += entry.CacheCreationTokens;
                        todayCacheRead += entry.CacheReadTokens;
                    }

                    weekInput += entry.InputTokens;
                    weekOutput += entry.OutputTokens;
                }
            }
            catch (Exception)
            {
                // Skip unreadable / locked files silently — partial data is preferable to a hard failure.
            }
        }

        return new ClaudeCodeUsageReport(
            todayInput,
            todayOutput,
            todayCacheCreate,
            todayCacheRead,
            weekInput,
            weekOutput,
            filesScanned);
    }

    private readonly record struct UsageEntry(
        string? MessageId,
        DateTimeOffset Timestamp,
        long InputTokens,
        long OutputTokens,
        long CacheCreationTokens,
        long CacheReadTokens);

    private static bool TryParseLine(string line, out UsageEntry entry)
    {
        entry = default;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!message.TryGetProperty("usage", out var usage)
                || usage.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("timestamp", out var tsElem)
                || tsElem.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(
                    tsElem.GetString(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var ts))
            {
                return false;
            }

            string? messageId = null;
            if (message.TryGetProperty("id", out var idElem) && idElem.ValueKind == JsonValueKind.String)
            {
                messageId = idElem.GetString();
            }

            entry = new UsageEntry(
                messageId,
                ts,
                ReadLong(usage, "input_tokens"),
                ReadLong(usage, "output_tokens"),
                ReadLong(usage, "cache_creation_input_tokens"),
                ReadLong(usage, "cache_read_input_tokens"));
            return true;
        }
    }

    private static long ReadLong(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var element))
        {
            return 0;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var value) => Math.Max(0, value),
            _ => 0
        };
    }
}
