using System;
using System.Text;
using CodexBar.Core.Settings;
using CodexBar.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace CodexBar.WinUI.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }
    private readonly Action<AppSettings> onSave;

    public SettingsWindow(SettingsViewModel viewModel, Action<AppSettings> onSave)
    {
        ViewModel = viewModel;
        this.onSave = onSave;
        InitializeComponent();
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(540, 720));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(true, false);
        }

        // Force integer display for the refresh-interval NumberBox (no decimal point).
        RefreshMinutesNumberBox.NumberFormatter = new Windows.Globalization.NumberFormatting.DecimalFormatter
        {
            FractionDigits = 0,
            IntegerDigits = 1,
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        onSave(ViewModel.ToSettings());
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Show a captured-key prompt: the next key combination the user presses (while
    /// the dialog is focused) becomes the new hotkey. Esc cancels.
    /// </summary>
    private async void RebindHotkey_Click(object sender, RoutedEventArgs e)
    {
        var prompt = new TextBlock
        {
            Text = "Press a key combination... (Esc to cancel)",
            TextWrapping = TextWrapping.Wrap,
        };

        var dialog = new ContentDialog
        {
            Title = "Rebind global hotkey",
            Content = prompt,
            CloseButtonText = "Cancel",
            XamlRoot = Content.XamlRoot,
        };

        string? captured = null;

        void OnKeyDown(object _, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
        {
            var key = args.Key;
            // Ignore standalone modifier presses — wait for the user to also hit a non-modifier.
            if (IsModifierKey(key))
            {
                args.Handled = true;
                return;
            }

            if (key == VirtualKey.Escape)
            {
                dialog.Hide();
                args.Handled = true;
                return;
            }

            var combo = BuildComboString(key);
            if (combo is null) { args.Handled = true; return; }
            captured = combo;
            prompt.Text = $"Captured: {combo}";
            args.Handled = true;
            // Accept after a short pause so the user can see the captured combo.
            _ = DispatcherQueue.TryEnqueue(() => dialog.Hide());
        }

        dialog.KeyDown += OnKeyDown;
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            dialog.KeyDown -= OnKeyDown;
        }

        if (captured is not null && HotkeyParser.TryParse(captured, out _))
        {
            ViewModel.GlobalHotkey = captured;
        }
    }

    public void ShowHotkeyConflictTip()
    {
        try { HotkeyConflictTip.IsOpen = true; } catch { /* ignore */ }
    }

    private static bool IsModifierKey(VirtualKey key) =>
        key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
            or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static string? BuildComboString(VirtualKey key)
    {
        // Read current modifier state from the CoreWindow / InputKeyboardSource.
        bool ctrl = IsDown(VirtualKey.Control) || IsDown(VirtualKey.LeftControl) || IsDown(VirtualKey.RightControl);
        bool shift = IsDown(VirtualKey.Shift) || IsDown(VirtualKey.LeftShift) || IsDown(VirtualKey.RightShift);
        bool alt = IsDown(VirtualKey.Menu) || IsDown(VirtualKey.LeftMenu) || IsDown(VirtualKey.RightMenu);
        bool win = IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows);

        var name = KeyName(key);
        if (name is null) return null;

        var sb = new StringBuilder();
        if (ctrl) sb.Append("Ctrl+");
        if (alt) sb.Append("Alt+");
        if (shift) sb.Append("Shift+");
        if (win) sb.Append("Win+");
        sb.Append(name);
        return sb.ToString();
    }

    private static bool IsDown(VirtualKey key)
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    private static string? KeyName(VirtualKey key)
    {
        int v = (int)key;
        if (v is >= 0x41 and <= 0x5A) return ((char)v).ToString();      // A-Z
        if (v is >= 0x30 and <= 0x39) return ((char)v).ToString();      // 0-9
        if (v is >= 0x70 and <= 0x87) return "F" + (v - 0x6F);          // F1-F24
        return key switch
        {
            VirtualKey.Space => "Space",
            VirtualKey.Tab => "Tab",
            VirtualKey.Enter => "Enter",
            VirtualKey.Back => "Backspace",
            VirtualKey.Delete => "Delete",
            VirtualKey.Insert => "Insert",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            VirtualKey.Left => "Left",
            VirtualKey.Up => "Up",
            VirtualKey.Right => "Right",
            VirtualKey.Down => "Down",
            _ => null,
        };
    }
}
