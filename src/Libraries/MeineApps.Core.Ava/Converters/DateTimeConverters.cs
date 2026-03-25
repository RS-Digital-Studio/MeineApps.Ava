using System.Globalization;
using Avalonia.Data.Converters;

namespace MeineApps.Core.Ava.Converters;

/// <summary>
/// Formats a DateTime to a string
/// </summary>
public sealed class DateTimeFormatConverter : IValueConverter
{
    public static readonly DateTimeFormatConverter Instance = new();

    public string Format { get; set; } = "dd.MM.yyyy";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return string.Empty;

        var format = parameter as string ?? Format;
        return dt.ToString(format, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (DateTime.TryParse(value?.ToString(), culture, DateTimeStyles.None, out var result))
            return result;
        return DateTime.MinValue;
    }
}

/// <summary>
/// Formatiert eine DateTime als relative Zeitangabe (z.B. "vor 2 Stunden").
/// Erwartet UTC-Zeitstempel (DateTime.UtcNow Konvention).
/// HINWEIS: Texte sind kompakt/sprachneutral gehalten (Kurzform).
/// Für vollständige Lokalisierung wäre ein ILocalizationService nötig,
/// der in einem IValueConverter nicht per DI verfügbar ist.
/// </summary>
public sealed class RelativeTimeConverter : IValueConverter
{
    public static readonly RelativeTimeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return string.Empty;

        // UTC verwenden um korrekte Differenz zu berechnen
        var diff = DateTime.UtcNow - dt;

        if (diff.TotalSeconds < 60)
            return "< 1 min";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} h";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} d";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)} w";
        if (diff.TotalDays < 365)
            return $"{(int)(diff.TotalDays / 30)} mo";

        return $"{(int)(diff.TotalDays / 365)} y";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Formats a TimeSpan to a string
/// </summary>
public sealed class TimeSpanFormatConverter : IValueConverter
{
    public static readonly TimeSpanFormatConverter Instance = new();

    public string Format { get; set; } = @"hh\:mm\:ss";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan ts) return "00:00:00";

        var format = parameter as string ?? Format;
        return ts.ToString(format, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (TimeSpan.TryParse(value?.ToString(), culture, out var result))
            return result;
        return TimeSpan.Zero;
    }
}

/// <summary>
/// Formats a TimeSpan as human-readable duration
/// </summary>
public sealed class DurationConverter : IValueConverter
{
    public static readonly DurationConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan ts) return "0s";

        if (ts.TotalSeconds < 60)
            return $"{(int)ts.TotalSeconds}s";
        if (ts.TotalMinutes < 60)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        if (ts.TotalHours < 24)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";

        return $"{(int)ts.TotalDays}d {ts.Hours}h";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
