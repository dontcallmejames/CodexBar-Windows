using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CodexBar.WinApp.ViewModels;
using MediaColor = System.Windows.Media.Color;

namespace CodexBar.WinApp.Views;

public partial class PopoverWindow : Window
{
    public PopoverWindow(PopoverViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

public sealed class BooleanToBrushConverter : IValueConverter
{
    public static BooleanToBrushConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true
            ? new SolidColorBrush(MediaColor.FromRgb(47, 123, 246))
            : new SolidColorBrush(MediaColor.FromArgb(45, 255, 255, 255));

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
