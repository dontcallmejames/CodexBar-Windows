using CodexBar.Core.Settings;

namespace CodexBar.WinApp.Settings;

public interface ISettingsWriter
{
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
