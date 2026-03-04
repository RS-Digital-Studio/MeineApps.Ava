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
/// Formats a DateTime as relative time (e.g., "2 hours ago")
/// </summary>
public sealed class RelativeTimeConverter : IValueConverter
{
    public static readonly RelativeTimeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return string.Empty;

        var diff = DateTime.Now - dt;

        if (diff.TotalSeconds < 60)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} h ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} d ago";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)} w ago";
        if (diff.TotalDays < 365)
            return $"{(int)(diff.TotalDays / 30)} mo ago";

        return $"{(int)(diff.TotalDays / 365)} y ago";
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
