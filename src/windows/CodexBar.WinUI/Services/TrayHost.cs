using System;
using System.Drawing;
using CodexBar.Core.Tray;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

namespace CodexBar.WinUI.Services;

public sealed class TrayHost : IDisposable
{
    // Stable notify-icon identity. Windows 11 keys an icon's notification-area state
    // (including the user's "always show / promote out of the overflow" choice) on this
    // GUID. Without a fixed GUID, every build path / update looks like a brand-new icon,
    // so promotion never sticks and HKCU\Control Panel\NotifyIconSettings accumulates a
    // dead entry per path. A constant GUID collapses the installed app to a single
    // persistent entry whose promotion survives updates.
    private static readonly Guid TrayIconId = new("7C0E1A2B-3D4E-4F5A-8B6C-9D0E1F2A3B4C");

    private readonly TaskbarIcon icon = new();
    private Icon? currentIcon;

    public event EventHandler? LeftClick;

    public Action? OnSettingsClick { get; set; }
    public Action? OnAboutClick { get; set; }
    public Action? OnQuitClick { get; set; }

    public TrayHost()
    {
        icon.Id = TrayIconId;
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
        // NOTE: H.NotifyIcon.WinUI's ContextFlyout doesn't reliably route MenuFlyoutItem.Click
        // events in unpackaged WinUI 3 apps (no XAML root context). Use Command bindings instead
        // — those fire correctly without depending on the visual tree.
        var menu = new MenuFlyout();

        menu.Items.Add(new MenuFlyoutItem
        {
            Text = "Settings...",
            Command = new RelayCommand(() => OnSettingsClick?.Invoke())
        });
        menu.Items.Add(new MenuFlyoutItem
        {
            Text = "About CodexBar",
            Command = new RelayCommand(() => OnAboutClick?.Invoke())
        });
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(new MenuFlyoutItem
        {
            Text = "Quit",
            Command = new RelayCommand(() => OnQuitClick?.Invoke())
        });

        return menu;
    }

    private static string Truncate(string tooltip) =>
        tooltip.Length <= 127 ? tooltip : tooltip[..127];
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
