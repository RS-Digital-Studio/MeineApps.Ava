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
/// </summary>
public class WorkshopColorConverter : IValueConverter
{
    public static WorkshopColorConverter Instance { get; } = new();

    private static readonly Dictionary<WorkshopType, Color> Colors = new()
    {
        [WorkshopType.Carpenter] = Color.Parse("#A0522D"),
        [WorkshopType.Plumber] = Color.Parse("#0E7490"),
        [WorkshopType.Electrician] = Color.Parse("#F97316"),
        [WorkshopType.Painter] = Color.Parse("#EC4899"),
        [WorkshopType.Roofer] = Color.Parse("#DC2626"),
        [WorkshopType.Contractor] = Color.Parse("#EA580C"),
        [WorkshopType.Architect] = Color.Parse("#78716C"),
        [WorkshopType.GeneralContractor] = Color.Parse("#FFD700")
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not WorkshopType type)
            return new SolidColorBrush(Color.Parse("#D97706"));

        var color = Colors.GetValueOrDefault(type, Color.Parse("#D97706"));

        if (parameter is string param)
        {
            byte alpha = param switch
            {
                "bg" => 51,     // 20%
                "bg40" => 102,  // 40%
                "bg60" => 153,  // 60%
                _ => 255
            };
            return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        }

        return new SolidColorBrush(color);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
