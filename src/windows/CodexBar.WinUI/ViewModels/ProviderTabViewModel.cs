using CodexBar.Core.Models;

namespace CodexBar.WinUI.ViewModels;

public sealed record ProviderTabViewModel(
    UsageProvider Provider,
    string DisplayName,
    bool IsActive,
    bool IsStale);
