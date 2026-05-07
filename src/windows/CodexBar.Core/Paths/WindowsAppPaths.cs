namespace CodexBar.Core.Paths;

public sealed class WindowsAppPaths : IAppPaths
{
    private readonly string homeDirectory;
    private readonly string roamingAppData;
    private readonly string localAppData;

    public WindowsAppPaths()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    private WindowsAppPaths(string homeDirectory, string roamingAppData, string localAppData)
    {
        this.homeDirectory = homeDirectory;
        this.roamingAppData = roamingAppData;
        this.localAppData = localAppData;
    }

    public string SettingsFile => Path.Combine(roamingAppData, "CodexBar", "config.json");
    public string CacheDirectory => Path.Combine(localAppData, "CodexBar", "Cache");
    public string LogDirectory => Path.Combine(localAppData, "CodexBar", "Logs");
    public string ClaudeCredentialsJson => Path.Combine(homeDirectory, ".claude", ".credentials.json");
    public string GeminiSettingsJson => Path.Combine(homeDirectory, ".gemini", "settings.json");
    public string GeminiOAuthCredentialsJson => Path.Combine(homeDirectory, ".gemini", "oauth_creds.json");

    public string CodexAuthJson(string? codexHome) =>
        Path.Combine(string.IsNullOrWhiteSpace(codexHome) ? Path.Combine(homeDirectory, ".codex") : codexHome, "auth.json");

    public static WindowsAppPaths ForTest(string homeDirectory, string appDataRoot) =>
        new(homeDirectory, appDataRoot, appDataRoot);
}
