using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Konvertiert WorkshopType → farbiger SolidColorBrush.
/// ConverterParameter "bg" liefert 20% Opacity-Variante fuer Hintergruende.
/// ConverterParameter "bg40" liefert 40% Opacity.
/// ConverterParameter "bg60" liefert 60% Opacity.
/// Brushes werden gecacht (max 40 Einträge: 10 Typen × 4 Alpha-Varianten).
/// </summary>
public class WorkshopColorConverter : IValueConverter
{
    public static WorkshopColorConverter Instance { get; } = new();

    // Farben aus WorkshopTypeExtensions.GetColorHex() (zentrale Quelle)
    private static readonly Dictionary<WorkshopType, Color> Colors = BuildColorCache();

    private static Dictionary<WorkshopType, Color> BuildColorCache()
    {
        var cache = new Dictionary<WorkshopType, Color>();
        foreach (WorkshopType type in Enum.GetValues<WorkshopType>())
            cache[type] = Color.Parse(type.GetColorHex());
        return cache;
    }

    private static readonly SolidColorBrush FallbackBrush = new(Color.Parse("#D97706"));
    private static readonly Dictionary<(WorkshopType, byte), SolidColorBrush> _cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not WorkshopType type)
            return FallbackBrush;

        byte alpha = 255;
        if (parameter is string param)
        {
            alpha = param switch
            {
                "bg" => 51,     // 20%
                "bg40" => 102,  // 40%
                "bg60" => 153,  // 60%
                _ => 255
            };
        }

        var key = (type, alpha);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var color = Colors.GetValueOrDefault(type, Color.Parse("#D97706"));
        var brush = alpha == 255
            ? new SolidColorBrush(color)
            : new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));

        _cache[key] = brush;
        return brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
