using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FinanzRechner.Models;

namespace FinanzRechner.Converters;

/// <summary>
/// Konvertiert BudgetAlertLevel zu Farb-Brush (Safe: IncomeColor, Warning: WarningColor, Exceeded: ExpenseColor).
/// Nutzt TryGetResource für Theme-Unterstützung.
/// </summary>
public class AlertLevelToColorConverter : IValueConverter
{
    private static readonly IBrush SafeBrush = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush ExceededBrush = new SolidColorBrush(Color.Parse("#EF4444"));

    public static readonly AlertLevelToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BudgetAlertLevel alertLevel)
        {
            var resourceKey = alertLevel switch
            {
                BudgetAlertLevel.Safe => "IncomeColor",
                BudgetAlertLevel.Warning => "WarningColor",
                BudgetAlertLevel.Exceeded => "ExpenseColor",
                _ => "IncomeColor"
            };

            var app = Application.Current;
            if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var brush) && brush is IBrush b)
                return b;

            // Fallback-Farben
            return alertLevel switch
            {
                BudgetAlertLevel.Safe => SafeBrush,
                BudgetAlertLevel.Warning => WarningBrush,
                BudgetAlertLevel.Exceeded => ExceededBrush,
                _ => SafeBrush
            };
        }

        // Standard-Fallback
        var application = Application.Current;
        if (application != null && application.TryGetResource("IncomeColor", application.ActualThemeVariant, out var defaultBrush) && defaultBrush is IBrush db)
            return db;
        return SafeBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
