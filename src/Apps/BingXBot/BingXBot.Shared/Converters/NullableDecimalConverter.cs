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
        // Unterstützt decimal UND decimal? (für Preis-Anzeige und SL/TP-TextBoxen)
        if (value is not decimal d) return "";

        // KEINE Exponentialnotation! Krypto-Preise wie 0.00005625 müssen als Dezimalzahl angezeigt werden.
        if (d == 0) return "0";
        var str = d.ToString("F20", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        return str;
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
