using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Konvertiert einen String (z.B. "Star", "Screwdriver") in MaterialIconKind.
/// Wird verwendet um Icon-Strings aus ViewModels als MaterialIcon darzustellen.
/// </summary>
public class StringToMaterialIconKindConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && Enum.TryParse<MaterialIconKind>(s, out var kind))
            return kind;
        return MaterialIconKind.HelpCircle;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
