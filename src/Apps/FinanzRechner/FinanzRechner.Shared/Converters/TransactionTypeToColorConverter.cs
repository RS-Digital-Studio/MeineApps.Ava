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
    private static readonly IBrush ExpenseBrush = ExpenseBrush;
    private static readonly IBrush IncomeBrush = IncomeBrush;

    public static readonly TransactionTypeToColorConverter Instance = new();

    private static readonly IBrush FallbackGray = new SolidColorBrush(Color.Parse("#9E9E9E"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TransactionType type)
            return FallbackGray;

        var resourceKey = type == TransactionType.Expense ? "ExpenseColor" : "IncomeColor";

        var app = Application.Current;
        if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var brush) && brush is IBrush b)
            return b;

        // Fallback colors
        return type == TransactionType.Expense
            ? ExpenseBrush
            : IncomeBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
