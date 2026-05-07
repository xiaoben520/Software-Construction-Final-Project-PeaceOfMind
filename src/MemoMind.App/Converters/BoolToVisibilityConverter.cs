using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MemoMind.App.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        if (parameter is string param && param.Contains("invert", StringComparison.OrdinalIgnoreCase))
        {
            boolValue = !boolValue;
        }
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}
