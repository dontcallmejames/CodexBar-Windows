using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CodexBar.WinApp.ViewModels;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace CodexBar.WinApp.Views;

public partial class PopoverWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer indicatorTimer;

    public PopoverWindow(PopoverViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        indicatorTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        indicatorTimer.Tick += (_, _) =>
        {
            if (DataContext is PopoverViewModel vm)
            {
                vm.RefreshLiveIndicator();
            }
        };
        Loaded += (_, _) => indicatorTimer.Start();
        Closed += (_, _) => indicatorTimer.Stop();
    }
}

public sealed class BooleanToBrushConverter : IValueConverter
{
    public static BooleanToBrushConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? new SolidColorBrush(MediaColor.FromRgb(47, 123, 246))
            : MediaBrushes.Transparent;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BooleanToForegroundBrushConverter : IValueConverter
{
    public static BooleanToForegroundBrushConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? MediaBrushes.White
            : new SolidColorBrush(MediaColor.FromRgb(112, 105, 132));

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
