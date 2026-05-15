using System.Windows.Input;

namespace CodexBar.WinApp.ViewModels;

public sealed record PopoverMetricViewModel(
    string Title,
    double ProgressPercent,
    string PercentText,
    string ResetText,
    string ProgressColor);

public sealed record PopoverFooterRowViewModel(
    string Title,
    string IconGlyph,
    bool HasIcon,
    ICommand Command);
