using System.Globalization;
using System.Windows.Data;

namespace MemoMind.App.Converters;

public class ProgressToOverlayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var progress = value is int i ? (double)i : (value is double d ? d : 0.0);
        var totalWidth = parameter is string s && double.TryParse(s, out var w) ? w : 340.0;
        return progress / 100.0 * totalWidth;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
