using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BomberBlast.Converters;

/// <summary>
/// Konvertiert bool (IsDiscovered) → Hintergrundfarbe für Sammlungs-Karten.
/// Entdeckt = Weiß (mit geringer Opacity), Nicht entdeckt = Dunkelgrau.
/// </summary>
public class BoolToCardBackgroundConverter : IValueConverter
{
    public static readonly BoolToCardBackgroundConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Color.Parse("#FFFFFF") : Color.Parse("#404040");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
