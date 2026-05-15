using System;
using System.Drawing;
using CodexBar.Core.Tray;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

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
        icon.ContextFlyout = BuildContextMenu();
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

    private MenuFlyout BuildContextMenu()
    {
        var menu = new MenuFlyout();

        var settings = new MenuFlyoutItem { Text = "Settings..." };
        settings.Click += (_, _) => OnSettingsClick?.Invoke();
        menu.Items.Add(settings);

        var about = new MenuFlyoutItem { Text = "About CodexBar" };
        about.Click += (_, _) => OnAboutClick?.Invoke();
        menu.Items.Add(about);

        menu.Items.Add(new MenuFlyoutSeparator());

        var quit = new MenuFlyoutItem { Text = "Quit" };
        quit.Click += (_, _) => OnQuitClick?.Invoke();
        menu.Items.Add(quit);

        return menu;
    }

    private static string Truncate(string tooltip) =>
        tooltip.Length <= 63 ? tooltip : tooltip[..63];
}

#pragma warning disable CS0067 // The event 'RelayCommand.CanExecuteChanged' is never used
internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action action;
    public RelayCommand(Action action) { this.action = action; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
}
#pragma warning restore CS0067
