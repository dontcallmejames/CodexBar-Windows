using System.Globalization;
using CodexBar.Core.Providers.Claude;

namespace CodexBar.Tests;

[TestClass]
public sealed class ClaudeCodeLocalUsageScannerTests
{
    private string tempRoot = string.Empty;

    [TestInitialize]
    public void Init()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "ccusage-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore locked/leftover files.
        }
    }

    private static string AssistantLine(string id, string timestampIso, long input, long output, long cacheCreate = 0, long cacheRead = 0)
    {
        return "{\"type\":\"assistant\",\"message\":{\"id\":\"" + id + "\",\"usage\":{\"input_tokens\":" + input
            + ",\"output_tokens\":" + output
            + ",\"cache_creation_input_tokens\":" + cacheCreate
            + ",\"cache_read_input_tokens\":" + cacheRead
            + "}},\"timestamp\":\"" + timestampIso + "\"}";
    }

    private static string UserLine(string timestampIso) =>
        "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":\"hi\"},\"timestamp\":\"" + timestampIso + "\"}";

    [TestMethod]
    public void MissingRootReturnsEmpty()
    {
        var scanner = new ClaudeCodeLocalUsageScanner(Path.Combine(tempRoot, "does-not-exist"));
        var report = scanner.Scan(DateTimeOffset.Now);
        Assert.AreSame(ClaudeCodeUsageReport.Empty, report);
    }

    [TestMethod]
    public void EmptyProjectsDirReturnsEmpty()
    {
        var scanner = new ClaudeCodeLocalUsageScanner(tempRoot);
        var report = scanner.Scan(DateTimeOffset.Now);
        Assert.AreEqual(0, report.TodayTotalTokens);
        Assert.AreEqual(0, report.Last7DaysTotalTokens);
        Assert.AreEqual(0, report.SessionFilesScanned);
    }

    [TestMethod]
    public void MalformedOnlyFileReturnsEmptyButCountsFile()
    {
        var asOf = DateTimeOffset.Now;
        var project = Path.Combine(tempRoot, "project-a");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "session-1.jsonl"),
            "not json at all\n{\"oops\":\n");

        var scanner = new ClaudeCodeLocalUsageScanner(tempRoot);
        var report = scanner.Scan(asOf);

        Assert.AreEqual(0, report.TodayTotalTokens);
        Assert.AreEqual(1, report.SessionFilesScanned);
    }

    [TestMethod]
    public void SumsTodaysAssistantUsageAndIgnoresUserLines()
    {
        var asOf = new DateTimeOffset(2026, 5, 18, 14, 0, 0, TimeSpan.Zero).ToLocalTime();
        var todayTs = asOf.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

        var project = Path.Combine(tempRoot, "project-a");
        Directory.CreateDirectory(project);
        File.WriteAllLines(Path.Combine(project, "session-1.jsonl"), new[]
        {
            AssistantLine("msg_1", todayTs, 100, 200, 10, 20),
            UserLine(todayTs),
            "{not json",
            AssistantLine("msg_2", todayTs, 5, 7)
        });

        var scanner = new ClaudeCodeLocalUsageScanner(tempRoot);
        var report = scanner.Scan(asOf);

        Assert.AreEqual(105, report.TodayInputTokens);
        Assert.AreEqual(207, report.TodayOutputTokens);
        Assert.AreEqual(10, report.TodayCacheCreationTokens);
        Assert.AreEqual(20, report.TodayCacheReadTokens);
        Assert.AreEqual(342, report.TodayTotalTokens);
        Assert.AreEqual(1, report.SessionFilesScanned);
    }

    [TestMethod]
    public void DeduplicatesByMessageIdAcrossSessions()
    {
        var asOf = new DateTimeOffset(2026, 5, 18, 14, 0, 0, TimeSpan.Zero).ToLocalTime();
        var ts = asOf.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        var line = AssistantLine("msg_dup", ts, 50, 75);

        var pA = Path.Combine(tempRoot, "project-a");
        var pB = Path.Combine(tempRoot, "project-b");
        Directory.CreateDirectory(pA);
        Directory.CreateDirectory(pB);
        File.WriteAllLines(Path.Combine(pA, "s1.jsonl"), new[] { line });
        File.WriteAllLines(Path.Combine(pB, "s2.jsonl"), new[] { line });

        var scanner = new ClaudeCodeLocalUsageScanner(tempRoot);
        var report = scanner.Scan(asOf);

        Assert.AreEqual(50, report.TodayInputTokens);
        Assert.AreEqual(75, report.TodayOutputTokens);
        Assert.AreEqual(2, report.SessionFilesScanned);
    }

    [TestMethod]
    public void SeparatesTodayFromSevenDayRollupAndSkipsStaleFiles()
    {
        var asOf = new DateTimeOffset(2026, 5, 18, 14, 0, 0, TimeSpan.Zero).ToLocalTime();
        var today = asOf.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        var threeDaysAgo = asOf.UtcDateTime.AddDays(-3).ToString("o", CultureInfo.InvariantCulture);
        var tenDaysAgo = asOf.UtcDateTime.AddDays(-10).ToString("o", CultureInfo.InvariantCulture);

        var project = Path.Combine(tempRoot, "project-a");
        Directory.CreateDirectory(project);

        File.WriteAllLines(Path.Combine(project, "recent.jsonl"), new[]
        {
            AssistantLine("a", today, 10, 20),
            AssistantLine("b", threeDaysAgo, 100, 200)
        });

        var stalePath = Path.Combine(project, "stale.jsonl");
        File.WriteAllLines(stalePath, new[]
        {
            AssistantLine("c", tenDaysAgo, 9999, 9999)
        });
        // Set the stale file's mtime relative to asOf (NOT real wall-clock time) so the test
        // stays deterministic. The scanner's stale cutoff is asOf - MaxAge; anchoring to
        // DateTime.UtcNow made this a time bomb that stopped skipping the file once the real
        // date drifted past the hardcoded asOf.
        File.SetLastWriteTimeUtc(stalePath, asOf.UtcDateTime.AddDays(-30));

        var scanner = new ClaudeCodeLocalUsageScanner(tempRoot);
        var report = scanner.Scan(asOf);

        Assert.AreEqual(10, report.TodayInputTokens);
        Assert.AreEqual(20, report.TodayOutputTokens);
        Assert.AreEqual(110, report.Last7DaysInputTokens);
        Assert.AreEqual(220, report.Last7DaysOutputTokens);
        Assert.AreEqual(1, report.SessionFilesScanned);
    }

    [TestMethod]
    public void CancelledTokenReturnsWithoutThrowing()
    {
        var asOf = new DateTimeOffset(2026, 5, 18, 14, 0, 0, TimeSpan.Zero).ToLocalTime();
        var ts = asOf.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        var project = Path.Combine(tempRoot, "project");
        Directory.CreateDirectory(project);
        File.WriteAllLines(Path.Combine(project, "s.jsonl"), new[] { AssistantLine("x", ts, 1, 1) });

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var scanner = new ClaudeCodeLocalUsageScanner(tempRoot);
        var report = scanner.Scan(asOf, cts.Token);

        Assert.IsNotNull(report);
    }
}
