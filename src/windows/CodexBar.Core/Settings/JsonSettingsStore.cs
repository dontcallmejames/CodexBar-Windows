using System.Text.Json;

namespace CodexBar.Core.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string path;

    public JsonSettingsStore(string path)
    {
        this.path = path;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return AppSettings.Default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken) ?? AppSettings.Default;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
    }
}
