using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FinanzRechner.Models;

namespace FinanzRechner.Converters;

/// <summary>
/// Konvertiert BudgetAlertLevel zu BoxShadows für status-basiertes Glow auf Budget-Cards.
/// Safe: subtiler grüner Glow, Warning: gelber Glow, Exceeded: roter Glow.
/// </summary>
public class AlertLevelToBoxShadowConverter : IValueConverter
{
    public static readonly AlertLevelToBoxShadowConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BudgetAlertLevel alertLevel)
        {
            return alertLevel switch
            {
                BudgetAlertLevel.Safe => BoxShadows.Parse("0 0 8 0 #3022C55E"),
                BudgetAlertLevel.Warning => BoxShadows.Parse("0 0 8 0 #30F59E0B"),
                BudgetAlertLevel.Exceeded => BoxShadows.Parse("0 0 12 0 #40EF4444"),
                _ => BoxShadows.Parse("0 0 8 0 #3022C55E")
            };
        }

        return BoxShadows.Parse("0 0 8 0 #3022C55E");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
