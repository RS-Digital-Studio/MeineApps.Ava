using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MeineApps.Core.Ava.Converters;

/// <summary>
/// Konvertiert einen Hex-Farbstring (#RRGGBB) in einen SolidColorBrush.
/// Wird verwendet für dynamische Farben (Gilden-Farben, Badge-Farben, etc.).
/// Statische Instance für x:Static Binding in XAML verfügbar.
/// </summary>
public class StringToColorBrushConverter : IValueConverter
{
    /// <summary>
    /// Statische Instanz für x:Static Binding in XAML.
    /// </summary>
    public static readonly StringToColorBrushConverter Instance = new();

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

/// <summary>
/// Konvertiert einen Hex-Farbstring (#RRGGBB) in ein Color-Objekt.
/// Wird verwendet wenn eine Color-Property (nicht Brush) gebunden werden muss,
/// z.B. bei SolidColorBrush.Color mit benutzerdefinierter Opacity.
/// </summary>
public class StringToColorConverter : IValueConverter
{
    /// <summary>
    /// Statische Instanz für x:Static Binding in XAML.
    /// </summary>
    public static readonly StringToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                return Color.Parse(hex);
            }
            catch
            {
                // Ungültiger Farbwert
            }
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
