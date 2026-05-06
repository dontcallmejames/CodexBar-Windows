using System.Drawing;
using System.Windows.Forms;

namespace CodexBar.Tray;

public sealed class TrayIconHost : IDisposable
{
    private const int NotifyIconTextLimit = 63;

    private readonly NotifyIcon notifyIcon;
    private Icon? currentIcon;

    public TrayIconHost(Action onLeftClick, Action onSettingsClick, Action onQuitClick)
    {
        notifyIcon = new NotifyIcon
        {
            Text = "CodexBar",
            Visible = true,
            ContextMenuStrip = BuildMenu(onSettingsClick, onQuitClick)
        };

        notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                onLeftClick();
            }
        };
    }

    public void Update(TrayDisplayModel model)
    {
        currentIcon?.Dispose();
        currentIcon = MeterIconRenderer.Render(model);
        notifyIcon.Icon = currentIcon;
        notifyIcon.Text = TruncateTooltip(model.Tooltip);
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        currentIcon?.Dispose();
    }

    private static ContextMenuStrip BuildMenu(Action onSettingsClick, Action onQuitClick)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => onSettingsClick());
        menu.Items.Add("Quit", null, (_, _) => onQuitClick());
        return menu;
    }

    private static string TruncateTooltip(string tooltip) =>
        tooltip.Length <= NotifyIconTextLimit ? tooltip : tooltip[..NotifyIconTextLimit];
}
