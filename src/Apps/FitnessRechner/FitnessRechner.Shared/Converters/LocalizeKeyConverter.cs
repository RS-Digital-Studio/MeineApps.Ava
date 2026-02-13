using System.Globalization;
using Avalonia.Data.Converters;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessRechner.Converters;

/// <summary>
/// Konvertiert einen RESX-Key-String in den lokalisierten Text.
/// Verwendung: {Binding TitleKey, Converter={StaticResource LocalizeKeyConverter}}
/// </summary>
public class LocalizeKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && !string.IsNullOrEmpty(key))
        {
            try
            {
                var loc = App.Services.GetRequiredService<ILocalizationService>();
                return loc.GetString(key) ?? key;
            }
            catch
            {
                return key;
            }
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
