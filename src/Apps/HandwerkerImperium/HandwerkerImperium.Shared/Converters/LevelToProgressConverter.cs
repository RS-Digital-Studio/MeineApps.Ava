using System.Globalization;
using Avalonia.Data.Converters;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Konvertiert Workshop-Level in einen Fortschrittswert (0.0 - 1.0) basierend auf dem Max-Level.
/// </summary>
public class LevelToProgressConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double progress = 0.0;

        // Unterstützt sowohl int (Level) als auch double (Position)
        if (value is int intValue)
        {
            progress = intValue;
        }
        else if (value is double doubleValue)
        {
            progress = doubleValue;
        }
        else
        {
            return 0.0;
        }

        // Multiplikator aus Parameter parsen (z.B. "300" für Pixel-Breite)
        double multiplier = 10.0; // Standard Max-Level
        if (parameter is string paramStr && double.TryParse(paramStr, out double parsedMultiplier))
        {
            multiplier = parsedMultiplier;
        }

        // Pixel-Positionierung (großer Multiplikator wie 300): direkt multiplizieren
        // Fortschrittsbalken (kleiner Multiplikator wie 10): Verhältnis berechnen
        if (multiplier > 100)
        {
            // Pixel-Positionierung: Fortschritt (0-1) * Breite
            return progress * multiplier;
        }
        else
        {
            // Fortschrittsverhältnis: Level / MaxLevel
            return Math.Min(1.0, progress / multiplier);
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
