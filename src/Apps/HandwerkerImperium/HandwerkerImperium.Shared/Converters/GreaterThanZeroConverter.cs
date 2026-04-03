using System.Globalization;
using Avalonia.Data.Converters;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Konvertiert einen numerischen Wert in einen Boolean (true wenn größer als Null).
/// Nützlich für Sichtbarkeits-Bindings basierend auf Zählern.
/// </summary>
public class GreaterThanZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            int intValue => intValue > 0,
            double doubleValue => doubleValue > 0,
            decimal decimalValue => decimalValue > 0,
            long longValue => longValue > 0,
            float floatValue => floatValue > 0,
            _ => false
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
