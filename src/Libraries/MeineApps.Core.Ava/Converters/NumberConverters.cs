using System.Globalization;
using Avalonia.Data.Converters;

namespace MeineApps.Core.Ava.Converters;

/// <summary>
/// Formats a number with specified decimal places
/// </summary>
public sealed class NumberFormatConverter : IValueConverter
{
    public static readonly NumberFormatConverter Instance = new();

    public int DecimalPlaces { get; set; } = 2;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return "0";

        var decimals = parameter is int dp ? dp : DecimalPlaces;
        var format = $"N{decimals}";

        return value switch
        {
            double dbl => dbl.ToString(format, culture),
            decimal dec => dec.ToString(format, culture),
            float f => f.ToString(format, culture),
            int i => i.ToString("N0", culture),
            long l => l.ToString("N0", culture),
            _ => value.ToString() ?? "0"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value?.ToString() ?? "0";
        if (double.TryParse(str, NumberStyles.Any, culture, out var result))
            return result;
        return 0.0;
    }
}

/// <summary>
/// Formats a number as currency
/// </summary>
public sealed class CurrencyConverter : IValueConverter
{
    public static readonly CurrencyConverter Instance = new();

    public string CurrencySymbol { get; set; } = "€";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return $"0.00 {CurrencySymbol}";

        var symbol = parameter as string ?? CurrencySymbol;

        return value switch
        {
            double d => $"{d:N2} {symbol}",
            decimal dec => $"{dec:N2} {symbol}",
            float f => $"{f:N2} {symbol}",
            int i => $"{i:N0} {symbol}",
            _ => $"{value} {symbol}"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value?.ToString()?.Replace(CurrencySymbol, "").Trim() ?? "0";
        if (decimal.TryParse(str, NumberStyles.Any, culture, out var result))
            return result;
        return 0m;
    }
}

/// <summary>
/// Formats a number as percentage
/// </summary>
public sealed class PercentageConverter : IValueConverter
{
    public static readonly PercentageConverter Instance = new();

    public int DecimalPlaces { get; set; } = 1;
    public bool MultiplyBy100 { get; set; } = true;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return "0%";

        double numValue = value switch
        {
            double d => d,
            decimal dec => (double)dec,
            float f => f,
            int i => i,
            _ => 0
        };

        if (MultiplyBy100) numValue *= 100;
        return $"{numValue.ToString($"N{DecimalPlaces}", culture)}%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value?.ToString()?.Replace("%", "").Trim() ?? "0";
        if (double.TryParse(str, NumberStyles.Any, culture, out var result))
            return MultiplyBy100 ? result / 100 : result;
        return 0.0;
    }
}

/// <summary>
/// Checks if a number is greater than zero
/// </summary>
public sealed class IsPositiveConverter : IValueConverter
{
    public static readonly IsPositiveConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            double d => d > 0,
            decimal dec => dec > 0,
            float f => f > 0,
            int i => i > 0,
            long l => l > 0,
            _ => false
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
