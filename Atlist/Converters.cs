using System.Globalization;

namespace Atlist.Converters;

/// <summary>true → false and false → true, for "show this when that is hidden".</summary>
public class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}
