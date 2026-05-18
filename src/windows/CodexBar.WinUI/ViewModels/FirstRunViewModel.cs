using CodexBar.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodexBar.WinUI.ViewModels;

public sealed partial class FirstRunViewModel : ObservableObject
{
    private readonly AppSettings originalSettings;

    [ObservableProperty] private bool codexEnabled;
    [ObservableProperty] private bool claudeEnabled;
    [ObservableProperty] private bool cursorEnabled;
    [ObservableProperty] private bool geminiEnabled;
    [ObservableProperty] private bool copilotEnabled;

    public FirstRunViewModel(AppSettings settings)
    {
        originalSettings = settings;
        codexEnabled = settings.CodexEnabled;
        claudeEnabled = settings.ClaudeEnabled;
        cursorEnabled = settings.CursorEnabled;
        geminiEnabled = settings.GeminiEnabled;
        copilotEnabled = settings.CopilotEnabled;
    }

    public AppSettings ToSettings() => originalSettings with
    {
        CodexEnabled = CodexEnabled,
        ClaudeEnabled = ClaudeEnabled,
        CursorEnabled = CursorEnabled,
        GeminiEnabled = GeminiEnabled,
        CopilotEnabled = CopilotEnabled,
    };
}
