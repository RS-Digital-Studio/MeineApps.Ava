using System.Globalization;
using Avalonia.Data.Converters;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Konvertiert decimal-Geldbetrag in formatierten Anzeige-String.
/// Konsistente Schwellen wie MoneyFormatter: T >= 1T, B >= 1B, M >= 1M, K >= 1K.
/// Unterstützt negative Werte (Netto-Verlust).
/// </summary>
public class MoneyDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not decimal money)
            return "0 \u20AC";

        bool isNegative = money < 0;
        decimal abs = Math.Abs(money);
        string prefix = isNegative ? "\u2212" : "";

        return abs switch
        {
            >= 1_000_000_000_000 => $"{prefix}{abs / 1_000_000_000_000:F1}T \u20AC",
            >= 1_000_000_000 => $"{prefix}{abs / 1_000_000_000:F1}B \u20AC",
            >= 1_000_000 => $"{prefix}{abs / 1_000_000:F1}M \u20AC",
            >= 1_000 => $"{prefix}{abs / 1_000:F1}K \u20AC",
            _ => $"{prefix}{abs:N0} \u20AC"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
