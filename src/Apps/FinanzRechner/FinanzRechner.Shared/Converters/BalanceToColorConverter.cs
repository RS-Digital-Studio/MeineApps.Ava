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

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double balance)
            return FallbackGray;

        var app = Application.Current;

        if (balance > 0)
        {
            if (app != null && app.TryGetResource("IncomeColor", app.ActualThemeVariant, out var incomeBrush) && incomeBrush is IBrush ib)
                return ib;
            return new SolidColorBrush(Color.Parse("#4CAF50"));
        }

        if (balance < 0)
        {
            if (app != null && app.TryGetResource("ExpenseColor", app.ActualThemeVariant, out var expenseBrush) && expenseBrush is IBrush eb)
                return eb;
            return new SolidColorBrush(Color.Parse("#F44336"));
        }

        return FallbackGray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
