using System.Globalization;
using Avalonia.Data.Converters;

namespace BomberBlast.Converters;

/// <summary>
/// Konvertiert einen bool in eine Opacity-Wert: true = 0.25, false = 0.0.
/// Verwendet fuer Tab-Switcher (aktiver Tab bekommt 25% Hintergrund-Tint).
/// Der Parameter kann optional eine Opacity-Override sein (z.B. "0.5").
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            // Optionaler Opacity-Override via Parameter (z.B. ConverterParameter="0.5")
            double active = 0.25;
            if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
                active = p;
            return b ? active : 0.0;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
