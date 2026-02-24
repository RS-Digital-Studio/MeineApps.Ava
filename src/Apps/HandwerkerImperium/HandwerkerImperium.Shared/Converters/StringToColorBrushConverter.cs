using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Konvertiert einen Hex-Farbstring (#RRGGBB) in einen SolidColorBrush.
/// Wird verwendet für Gilden-Farben in der Icon-/Farb-Auswahl.
/// </summary>
public class StringToColorBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                return new SolidColorBrush(Color.Parse(hex));
            }
            catch
            {
                // Ungültiger Farbwert
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
