using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using BomberBlast.ViewModels;

namespace BomberBlast.Converters;

/// <summary>
/// Vergleicht <see cref="ActiveView"/>-Wert mit <c>ConverterParameter</c> (String).
/// Liefert <c>true</c> wenn die Werte uebereinstimmen, sonst <c>false</c>.
///
/// <para>Verwendung in XAML:
/// <code>
/// IsVisible="{Binding ActiveView, Converter={x:Static conv:ActiveViewEqualsConverter.Instance}, ConverterParameter=Game}"
/// </code>
/// </para>
///
/// <para>ConverterParameter wird beim ersten Aufruf in <see cref="ActiveView"/> geparst und gecached
/// (kein Performance-Overhead pro Frame).</para>
/// </summary>
public sealed class ActiveViewEqualsConverter : IValueConverter
{
    public static readonly ActiveViewEqualsConverter Instance = new();

    private readonly ConcurrentDictionary<string, ActiveView> _cache = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ActiveView current)
            return false;

        if (parameter is not string paramStr || string.IsNullOrEmpty(paramStr))
            return false;

        var expected = _cache.GetOrAdd(paramStr, static p =>
            Enum.TryParse<ActiveView>(p, out var v) ? v : ActiveView.None);

        return current == expected;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
