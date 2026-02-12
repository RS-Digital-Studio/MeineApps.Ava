using System.Globalization;
using Avalonia.Data.Converters;

namespace FinanzRechner.Converters;

/// <summary>
/// Konvertiert bool zu String mit TrueValue/FalseValue Properties.
/// Hinweis: Da dieser Converter setzbare Properties hat, kann kein Singleton-Instance-Pattern verwendet werden.
/// Stattdessen Instanzen in XAML-Ressourcen erstellen.
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    public string TrueValue { get; set; } = "True";
    public string FalseValue { get; set; } = "False";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueValue : FalseValue;
        }
        return FalseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
