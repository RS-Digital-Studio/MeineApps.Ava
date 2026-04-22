using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MeineApps.Core.Ava.Converters;

/// <summary>
/// Konvertiert einen Hex-Farbstring (#RRGGBB) in einen SolidColorBrush.
/// Wird verwendet für dynamische Farben (Gilden-Farben, Badge-Farben, etc.).
/// Statische Instance für x:Static Binding in XAML verfügbar.
/// </summary>
public sealed class StringToColorBrushConverter : IValueConverter
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
/// Konvertiert ein Color-Objekt in einen SolidColorBrush.
/// Wird verwendet wenn das ViewModel Color-Properties exponiert (MVVM-konform,
/// Color ist ein Value-Type ohne UI-Kopplung) und die View einen Brush braucht.
/// Vermeidet SolidColorBrush-Allokationen im ViewModel.
/// </summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    /// <summary>
    /// Statische Instanz für x:Static Binding in XAML.
    /// </summary>
    public static readonly ColorToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
            return new SolidColorBrush(color);
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
public sealed class StringToColorConverter : IValueConverter
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
