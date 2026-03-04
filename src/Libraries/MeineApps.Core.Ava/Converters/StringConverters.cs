using System.Globalization;
using Avalonia.Data.Converters;

namespace MeineApps.Core.Ava.Converters;

/// <summary>
/// Checks if string is null or empty
/// </summary>
public sealed class StringIsNullOrEmptyConverter : IValueConverter
{
    public static readonly StringIsNullOrEmptyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value?.ToString());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Checks if string is NOT null or empty
/// </summary>
public sealed class StringIsNotNullOrEmptyConverter : IValueConverter
{
    public static readonly StringIsNotNullOrEmptyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value?.ToString());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Formats a string using String.Format
/// </summary>
public sealed class StringFormatConverter : IValueConverter
{
    public static readonly StringFormatConverter Instance = new();

    public string Format { get; set; } = "{0}";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var format = parameter as string ?? Format;
        return string.Format(culture, format, value);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Truncates a string to a maximum length
/// </summary>
public sealed class StringTruncateConverter : IValueConverter
{
    public static readonly StringTruncateConverter Instance = new();

    public int MaxLength { get; set; } = 50;
    public string Suffix { get; set; } = "...";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var str = value?.ToString() ?? string.Empty;
        var maxLen = parameter is int len ? len : MaxLength;

        if (str.Length <= maxLen) return str;
        return str[..(maxLen - Suffix.Length)] + Suffix;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
