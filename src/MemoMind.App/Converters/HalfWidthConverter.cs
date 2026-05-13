using System.Globalization;
using System.Windows.Data;

namespace MemoMind.App.Converters;

public class HalfWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string threshold && double.TryParse(threshold, out var t))
        {
            var val = value is double d ? d : (value is int i ? i : 0.0);
            return val >= t ? 1.0 : 0.15;
        }
        if (value is int intVal)
            return intVal / 100.0;
        if (value is double doubleVal)
            return doubleVal / 100.0;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
