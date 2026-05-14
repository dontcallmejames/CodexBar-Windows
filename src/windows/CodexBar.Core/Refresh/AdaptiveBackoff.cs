namespace CodexBar.Core.Refresh;

public static class AdaptiveBackoff
{
    private static readonly TimeSpan Cap = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(20);

    public static TimeSpan Delay(int consecutiveFailures, TimeSpan? retryAfter = null)
    {
        if (consecutiveFailures <= 0 && retryAfter is null)
        {
            return TimeSpan.Zero;
        }

        var jitter = 1.0 + (Random.Shared.NextDouble() * 0.5 - 0.25);
        var exponential = Base.TotalSeconds * Math.Pow(2, Math.Max(0, consecutiveFailures - 1)) * jitter;
        var computed = TimeSpan.FromSeconds(Math.Min(exponential, Cap.TotalSeconds));
        return retryAfter is { } hint && hint > computed ? hint : computed;
    }
}
