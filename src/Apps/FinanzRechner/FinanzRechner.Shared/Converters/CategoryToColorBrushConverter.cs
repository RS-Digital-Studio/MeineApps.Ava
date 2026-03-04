using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FinanzRechner.Helpers;
using FinanzRechner.Models;

namespace FinanzRechner.Converters;

/// <summary>
/// Konvertiert ExpenseCategory zu SolidColorBrush (Kategorie-Farbe).
/// Optionaler Parameter "0.2" für semi-transparente Variante (Hintergrund).
/// </summary>
public class CategoryToColorBrushConverter : IValueConverter
{
    private static readonly IBrush FallbackGrayBrush = FallbackGrayBrush;

    public static readonly CategoryToColorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ExpenseCategory category)
            return FallbackGrayBrush;

        var skColor = CategoryLocalizationHelper.GetCategoryColor(category);
        byte alpha = 255;

        // Parameter als Opacity (z.B. "0.2" für 20% Transparenz)
        if (parameter is string opacityStr &&
            double.TryParse(opacityStr, CultureInfo.InvariantCulture, out var opacity))
        {
            alpha = (byte)(255 * Math.Clamp(opacity, 0, 1));
        }

        return new SolidColorBrush(new Color(alpha, skColor.Red, skColor.Green, skColor.Blue));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
