using System.Globalization;
using Avalonia.Data.Converters;
using FinanzRechner.Models;
using MeineApps.Core.Ava.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace FinanzRechner.Converters;

/// <summary>
/// Konvertiert RecurrencePattern zu lokalisiertem String.
/// </summary>
public class PatternToStringConverter : IValueConverter
{
    public static readonly PatternToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RecurrencePattern pattern)
            return value?.ToString();

        var localizationService = App.Services?.GetService<ILocalizationService>();
        if (localizationService == null)
            return pattern.ToString();

        var key = pattern switch
        {
            RecurrencePattern.Daily => "PatternDaily",
            RecurrencePattern.Weekly => "PatternWeekly",
            RecurrencePattern.Biweekly => "PatternBiweekly",
            RecurrencePattern.Monthly => "PatternMonthly",
            RecurrencePattern.Yearly => "PatternYearly",
            _ => null
        };

        return key != null ? (localizationService.GetString(key) ?? pattern.ToString()) : pattern.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
