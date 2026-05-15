using System;
using System.Drawing;
using CodexBar.Core.Tray;
using H.NotifyIcon;

namespace CodexBar.WinUI.Services;

public sealed class TrayHost : IDisposable
{
    private readonly TaskbarIcon icon = new();
    private Icon? currentIcon;

    public event EventHandler? LeftClick;

    public Action? OnSettingsClick { get; set; }
    public Action? OnAboutClick { get; set; }
    public Action? OnQuitClick { get; set; }

    public TrayHost()
    {
        icon.NoLeftClickDelay = true;
        icon.ToolTipText = "CodexBar";
        icon.LeftClickCommand = new RelayCommand(() => LeftClick?.Invoke(this, EventArgs.Empty));
    }

    public void Show() => icon.ForceCreate();

    public void Update(TrayDisplayModel model)
    {
        currentIcon?.Dispose();
        currentIcon = MeterIconRenderer.Render(model);
        icon.Icon = currentIcon;
        icon.ToolTipText = Truncate(model.Tooltip);
    }

    public void Dispose()
    {
        icon.Dispose();
        currentIcon?.Dispose();
    }

    private static string Truncate(string tooltip) =>
        tooltip.Length <= 63 ? tooltip : tooltip[..63];
}

internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action action;
    public RelayCommand(Action action) { this.action = action; }
#pragma warning disable 67 // CanExecuteChanged is required by ICommand but never raised for always-enabled commands
    public event EventHandler? CanExecuteChanged;
#pragma warning restore 67
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
