using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FinanzRechner.Converters;

/// <summary>
/// Konvertiert bool zu Brush aus Anwendungsressourcen.
/// TrueColorKey = Ressourcen-Schlüssel bei true, FalseColorKey = Ressourcen-Schlüssel bei false.
/// Hinweis: Da dieser Converter setzbare Properties hat, kann kein Singleton-Instance-Pattern verwendet werden.
/// Stattdessen Instanzen in XAML-Ressourcen erstellen.
/// </summary>
public class BoolToResourceColorConverter : IValueConverter
{
    public string TrueColorKey { get; set; } = "PrimaryColor";
    public string FalseColorKey { get; set; } = "ButtonBackgroundColor";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is true;
        var resourceKey = isTrue ? TrueColorKey : FalseColorKey;

        var app = Application.Current;
        if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var resourceValue) && resourceValue is IBrush brush)
        {
            return brush;
        }

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
