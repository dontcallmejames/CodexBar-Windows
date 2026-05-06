using CodexBar.Core.Models;

namespace CodexBar.WinApp.ViewModels;

public sealed record ProviderTabViewModel(
    UsageProvider Provider,
    string Title,
    string PercentText,
    bool IsActive,
    bool IsStale);
