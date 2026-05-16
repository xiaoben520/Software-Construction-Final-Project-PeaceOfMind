using System.Globalization;
using System.Windows.Data;

namespace MemoMind.App.Converters;

public class BoolToCollapseTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "收起" : "展开";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
