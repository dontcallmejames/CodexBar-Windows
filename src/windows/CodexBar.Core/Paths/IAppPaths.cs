namespace CodexBar.Core.Paths;

public interface IAppPaths
{
    string SettingsFile { get; }
    string CacheDirectory { get; }
    string LogDirectory { get; }
    string ClaudeCredentialsJson { get; }
    string GeminiSettingsJson { get; }
    string GeminiOAuthCredentialsJson { get; }
    string CodexAuthJson(string? codexHome);
}
