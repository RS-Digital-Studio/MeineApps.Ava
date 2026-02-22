using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace BomberBlast.Converters;

/// <summary>
/// Konvertiert einen String (z.B. "Sword", "Crown") in MaterialIconKind.
/// Wird verwendet um Icon-Strings aus ViewModels als MaterialIcon darzustellen.
/// </summary>
public class StringToMaterialIconKindConverter : IValueConverter
{
    public static readonly StringToMaterialIconKindConverter Instance = new();

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
