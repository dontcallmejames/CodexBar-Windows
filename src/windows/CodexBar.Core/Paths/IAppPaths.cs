namespace CodexBar.Core.Paths;

public interface IAppPaths
{
    string SettingsFile { get; }
    string CacheDirectory { get; }
    string LogDirectory { get; }
    string ClaudeCredentialsJson { get; }
    string CodexAuthJson(string? codexHome);
}
