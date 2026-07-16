using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SidebarExplorer.App.Converters;

// Scales a TreeViewItem row's vertical padding with the tree's FontSize, so
// Ctrl+/- zoom shrinks/grows row spacing in the same proportion as the text
// instead of leaving a fixed gap that looks increasingly out of place.
public class FontSizeToRowPaddingConverter : IValueConverter
{
    private const double DefaultFontSize = 12;
    private const double DefaultVerticalPadding = 3;
    private const double HorizontalPadding = 4;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double fontSize = value is double size ? size : DefaultFontSize;
        double verticalPadding = fontSize * (DefaultVerticalPadding / DefaultFontSize);
        return new Thickness(HorizontalPadding, verticalPadding, HorizontalPadding, verticalPadding);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
