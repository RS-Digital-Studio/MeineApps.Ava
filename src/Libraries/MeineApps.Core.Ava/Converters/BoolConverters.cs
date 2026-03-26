using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MeineApps.Core.Ava.Converters;

/// <summary>
/// Converts bool to visibility (visible/collapsed)
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        if (Invert) boolValue = !boolValue;
        return boolValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        if (Invert) boolValue = !boolValue;
        return boolValue;
    }
}

/// <summary>
/// Converts bool to string (true/false values)
/// </summary>
public sealed class BoolToStringConverter : IValueConverter
{
    public static readonly BoolToStringConverter Instance = new();

    public string TrueValue { get; set; } = "True";
    public string FalseValue { get; set; } = "False";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueValue : FalseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() == TrueValue;
    }
}

/// <summary>
/// Converts bool to brush (for dynamic coloring)
/// </summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    public IBrush TrueBrush { get; set; } = Brushes.Green;
    public IBrush FalseBrush { get; set; } = Brushes.Red;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueBrush : FalseBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Konvertiert bool zu Opacity-Wert.
/// Standard: true = 1.0 (sichtbar), false = 0.4 (abgedunkelt).
/// TrueOpacity/FalseOpacity konfigurierbar.
/// </summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public double TrueOpacity { get; set; } = 1.0;
    public double FalseOpacity { get; set; } = 0.4;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueOpacity : FalseOpacity;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not true;
    }
}
