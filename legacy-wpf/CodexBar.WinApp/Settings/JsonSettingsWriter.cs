using CodexBar.Core.Settings;

namespace CodexBar.WinApp.Settings;

public sealed class JsonSettingsWriter : ISettingsWriter
{
    private readonly JsonSettingsStore store;

    public JsonSettingsWriter(JsonSettingsStore store)
    {
        this.store = store;
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) =>
        store.SaveAsync(settings, cancellationToken);
}
