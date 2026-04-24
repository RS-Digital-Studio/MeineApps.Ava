using Avalonia.Data.Converters;
using System.Globalization;

namespace BingXBot.Converters;

/// <summary>
/// 24.04.2026: Mappt bool (IsStale) auf Opacity-double — true → 0.40, false → 1.0.
/// Wird im Dashboard verwendet um die SK-Ampel-Tabelle visuell zu dimmen wenn der
/// Watchdog meldet dass die Engine nicht aktiv ist (Banner zeigt zusaetzlich den Hint).
/// </summary>
public sealed class StaleOpacityConverter : IValueConverter
{
    public static readonly StaleOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0.40 : 1.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
