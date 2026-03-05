using System.Globalization;
using Avalonia.Data.Converters;
using BomberBlast.Icons;

namespace BomberBlast.Converters;

/// <summary>
/// Konvertiert einen String (z.B. "Sword", "Crown") in GameIconKind.
/// Wird verwendet um Icon-Strings aus ViewModels als GameIcon darzustellen.
/// </summary>
public class StringToGameIconKindConverter : IValueConverter
{
    public static readonly StringToGameIconKindConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && Enum.TryParse<GameIconKind>(s, out var kind))
            return kind;
        return GameIconKind.HelpCircle;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
