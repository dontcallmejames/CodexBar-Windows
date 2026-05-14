namespace CodexBar.WinUI.Services;

public enum CodexBarTheme { Light, Dark }

public enum ThemePreference { System, Light, Dark }

public sealed class ThemeListener
{
    private readonly Func<CodexBarTheme> probeSystem;
    private ThemePreference preference = ThemePreference.System;

    public ThemeListener(Func<CodexBarTheme> probeSystem)
    {
        this.probeSystem = probeSystem;
    }

    public ThemePreference UserPreference
    {
        get => preference;
        set
        {
            if (preference == value) return;
            preference = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public CodexBarTheme Effective => preference switch
    {
        ThemePreference.Light => CodexBarTheme.Light,
        ThemePreference.Dark => CodexBarTheme.Dark,
        _ => probeSystem(),
    };

    public event EventHandler? Changed;

    public void Refresh() => Changed?.Invoke(this, EventArgs.Empty);
}
