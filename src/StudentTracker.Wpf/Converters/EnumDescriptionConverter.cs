using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace StudentTracker.Wpf.Converters;

public class EnumDescriptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;
        var type = value.GetType();
        if (!type.IsEnum) return value.ToString();
        var member = type.GetMember(value.ToString()!).FirstOrDefault();
        var attribute = member?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
