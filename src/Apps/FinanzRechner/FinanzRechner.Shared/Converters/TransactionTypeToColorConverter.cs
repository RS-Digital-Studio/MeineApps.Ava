using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FinanzRechner.Models;

namespace FinanzRechner.Converters;

/// <summary>
/// Converts TransactionType to color brush (Expense: ExpenseColor, Income: IncomeColor)
/// Uses TryFindResource for theme support
/// </summary>
public class TransactionTypeToColorConverter : IValueConverter
{
    private static readonly IBrush ExpenseBrush = new SolidColorBrush(Color.Parse("#EF4444"));
    private static readonly IBrush IncomeBrush = new SolidColorBrush(Color.Parse("#22C55E"));

    public static readonly TransactionTypeToColorConverter Instance = new();

    private static readonly IBrush FallbackGray = new SolidColorBrush(Color.Parse("#9E9E9E"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TransactionType type)
            return FallbackGray;

        var resourceKey = type switch
        {
            TransactionType.Expense => "ExpenseColor",
            TransactionType.Income => "IncomeColor",
            _ => (string?)null // Transfer und zukünftige Typen → neutral
        };

        if (resourceKey != null)
        {
            var app = Application.Current;
            if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var brush) && brush is IBrush b)
                return b;
        }

        // Fallback: Rot für Ausgaben, Grün für Einnahmen, Grau für Transfers
        return type switch
        {
            TransactionType.Expense => ExpenseBrush,
            TransactionType.Income => IncomeBrush,
            _ => FallbackGray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
