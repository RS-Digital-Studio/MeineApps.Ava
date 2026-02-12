using System.Globalization;
using Avalonia.Data.Converters;

namespace BomberBlast.Converters;

/// <summary>
/// Konvertiert bool → Opacity.
/// True = 1.0 (volle Sichtbarkeit), False = 0.4 (abgedunkelt).
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? 1.0 : 0.4;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
