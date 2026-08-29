using System.Globalization;
using System.Windows.Data;

namespace StudentTracker.Wpf.Converters;

public class InvertedBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? false : true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? false : true;
    }
}
