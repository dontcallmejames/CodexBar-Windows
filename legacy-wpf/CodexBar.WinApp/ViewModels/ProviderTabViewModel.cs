using CodexBar.Core.Models;

namespace CodexBar.WinApp.ViewModels;

public sealed record ProviderTabViewModel(
    UsageProvider Provider,
    string Title,
    string PercentText,
    double ProgressPercent,
    string IconGeometry,
    string ProgressColor,
    bool IsActive,
    bool IsStale);
