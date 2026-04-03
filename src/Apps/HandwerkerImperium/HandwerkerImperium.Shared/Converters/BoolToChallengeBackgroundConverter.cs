using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Konvertiert einen bool-Wert zu einer Hintergrundfarbe für Challenge-Chips.
/// Aktiv: Gold-Hintergrund (#30FFD700), Inaktiv: Transparent.
/// </summary>
public class BoolToChallengeBackgroundConverter : IValueConverter
{
    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#30FFD700"));
    private static readonly IBrush InactiveBrush = Brushes.Transparent;

    public static readonly BoolToChallengeBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? ActiveBrush : InactiveBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
