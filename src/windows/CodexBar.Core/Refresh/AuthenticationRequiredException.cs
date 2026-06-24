namespace CodexBar.Core.Refresh;

public sealed class AuthenticationRequiredException : Exception
{
    public AuthenticationRequiredException(string message) : base(message) { }
}
