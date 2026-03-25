using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FinanzRechner.Converters;

/// <summary>
/// Konvertiert Bilanzwert zu Farb-Brush (positiv: IncomeColor, negativ: ExpenseColor, null: Grau).
/// Nutzt TryGetResource für Theme-Unterstützung.
/// </summary>
public class BalanceToColorConverter : IValueConverter
{
    public static readonly BalanceToColorConverter Instance = new();

    private static readonly IBrush FallbackGray = new SolidColorBrush(Color.Parse("#9E9E9E"));
    private static readonly IBrush FallbackIncome = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly IBrush FallbackExpense = new SolidColorBrush(Color.Parse("#EF4444"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double balance)
            return FallbackGray;

        var app = Application.Current;

        if (balance > 0)
        {
            if (app != null && app.TryGetResource("IncomeColor", app.ActualThemeVariant, out var incomeBrush) && incomeBrush is IBrush ib)
                return ib;
            return FallbackIncome;
        }

        if (balance < 0)
        {
            if (app != null && app.TryGetResource("ExpenseColor", app.ActualThemeVariant, out var expenseBrush) && expenseBrush is IBrush eb)
                return eb;
            return FallbackExpense;
        }

        return FallbackGray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
