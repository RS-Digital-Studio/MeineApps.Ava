using System.Globalization;
using Avalonia.Data.Converters;
using WorkTimePro.Resources.Strings;

namespace WorkTimePro.Converters;

/// <summary>
/// Invertiert einen Boolean-Wert
/// </summary>
public class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

/// <summary>
/// Konvertiert einen Integer (> 0) zu Bool
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert einen String (nicht leer/null) zu Bool
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert ein Objekt (nicht null) zu Bool
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Prüft ob ein String nicht null oder leer ist.
/// Delegiert intern an StringToBoolConverter (identische Logik, existiert nur für XAML-Kompatibilität).
/// </summary>
public class StringNotNullConverter : IValueConverter
{
    private static readonly StringToBoolConverter _inner = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _inner.Convert(value, targetType, parameter, culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Konvertiert Rundungsminuten (0/5/10/15/30) in einen Anzeige-String
/// </summary>
public class RoundingDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int minutes)
        {
            return minutes == 0
                ? AppStrings.NoRounding
                : string.Format(AppStrings.MinutesShortFormat, minutes);
        }
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
