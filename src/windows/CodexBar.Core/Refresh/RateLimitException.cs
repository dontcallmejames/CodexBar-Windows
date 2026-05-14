namespace CodexBar.Core.Refresh;

public sealed class RateLimitException : Exception
{
    public RateLimitException(string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan? RetryAfter { get; }
}
