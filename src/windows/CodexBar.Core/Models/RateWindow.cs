namespace CodexBar.Core.Models;

public sealed record RateWindow(
    string Id,
    string Title,
    double UsedPercent,
    DateTimeOffset? ResetsAt,
    int? WindowMinutes)
{
    public double PercentLeft => Math.Clamp(100.0 - UsedPercent, 0.0, 100.0);
}
