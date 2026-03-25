using System.Globalization;
using Avalonia.Data.Converters;

namespace HandwerkerImperium.Converters;

/// <summary>
/// Konvertiert decimal-Geldbetrag in formatierten Anzeige-String.
/// ConverterParameter: "perhour" → €/h, "persecond" → €/s, "~" → Prefix "~", sonst kompakt.
/// </summary>
public class MoneyDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not decimal money)
            return "0 \u20AC";

        var param = parameter as string ?? "";
        return param switch
        {
            "perhour" => Helpers.MoneyFormatter.FormatPerHour(money),
            "persecond" => Helpers.MoneyFormatter.FormatPerSecond(money),
            "~" => $"~{Helpers.MoneyFormatter.FormatCompact(money)}",
            _ => Helpers.MoneyFormatter.FormatCompact(money)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
