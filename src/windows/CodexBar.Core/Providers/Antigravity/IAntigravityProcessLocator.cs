namespace CodexBar.Core.Providers.Antigravity;

/// <summary>
/// One discovered Antigravity language-server endpoint. <see cref="CsrfToken"/> is empty for the
/// <c>agy</c> CLI (which requires no token) and the IDE token otherwise.
/// </summary>
public sealed record AntigravityCandidate(
    int Pid,
    IReadOnlyList<int> LoopbackPorts,
    string CsrfToken,
    int? ExtensionServerPort,
    string? ExtensionServerCsrfToken,
    bool IsCli);

public interface IAntigravityProcessLocator
{
    /// <summary>Returns every running Antigravity language server found, best candidate first.</summary>
    IReadOnlyList<AntigravityCandidate> FindCandidates();
}
