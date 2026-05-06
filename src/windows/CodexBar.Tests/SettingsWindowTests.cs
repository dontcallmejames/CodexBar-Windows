using CodexBar.Core.Settings;
using CodexBar.WinApp.Settings;
using CodexBar.WinApp.ViewModels;
using CodexBar.WinApp.Views;

namespace CodexBar.Tests;

[TestClass]
public sealed class SettingsWindowTests
{
    [TestMethod]
    public async Task SaveSettingsAsyncReturnsFailureWhenWriterThrows()
    {
        var writer = new ThrowingSettingsWriter();
        var viewModel = new SettingsViewModel(AppSettings.Default);

        var result = await SettingsWindow.SaveSettingsAsync(writer, viewModel, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsInstanceOfType<InvalidOperationException>(result.Error);
    }

    private sealed class ThrowingSettingsWriter : ISettingsWriter
    {
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("save failed");
    }
}
