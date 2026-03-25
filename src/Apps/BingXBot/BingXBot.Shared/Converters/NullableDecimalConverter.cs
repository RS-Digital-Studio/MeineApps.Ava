using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace BingXBot.Converters;

/// <summary>
/// Konvertiert decimal? in string und zurueck fuer TextBox-Bindings.
/// Leeres Feld → null, ungueltiger Input → BindingNotification.Error (kein Crash),
/// gueltiger Input → decimal-Wert.
/// </summary>
public class NullableDecimalConverter : IValueConverter
{
    public static readonly NullableDecimalConverter Instance = new();

    /// <summary>Anzahl Nachkommastellen fuer die Anzeige (Standard: 2).</summary>
    public int DecimalPlaces { get; set; } = 2;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            // G-Format: Zeigt signifikante Stellen statt fixe Dezimalstellen.
            // Verhindert Rundung auf 0.00 bei Micro-Cap Token-Preisen (z.B. 0.0000301)
            return d.ToString("G10", CultureInfo.InvariantCulture);
        }

        // null → leerer String (zeigt Watermark)
        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string str)
            return null;

        var trimmed = str.Trim();

        // Leeres Feld → null (SL/TP deaktiviert)
        if (string.IsNullOrEmpty(trimmed))
            return (decimal?)null;

        // Komma und Punkt akzeptieren
        trimmed = trimmed.Replace(',', '.');

        if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            return (decimal?)result;

        // Ungueltiger Input: Binding-Fehler signalisieren (Avalonia zeigt roten Rahmen)
        return new BindingNotification(
            new FormatException($"'{str}' ist keine gueltige Zahl"),
            BindingErrorType.DataValidationError);
    }
}
