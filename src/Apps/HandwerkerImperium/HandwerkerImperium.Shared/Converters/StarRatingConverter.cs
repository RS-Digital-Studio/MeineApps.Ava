using System.Globalization;
using Avalonia.Data.Converters;
using HandwerkerImperium.Helpers;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Converts star rating (1-3) to MDI star glyph string.
/// Use with FontFamily="MDI" on the TextBlock.
/// </summary>
public class StarRatingConverter : IValueConverter
{
    // Vorberechnete Strings vermeiden String-Concatenation bei jedem Convert-Aufruf
    private static readonly string OneStar = Icons.Star;
    private static readonly string TwoStars = Icons.Star + Icons.Star;
    private static readonly string ThreeStars = Icons.Star + Icons.Star + Icons.Star;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int stars)
            return "";

        return stars switch
        {
            1 => OneStar,
            2 => TwoStars,
            3 => ThreeStars,
            _ => ""
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
