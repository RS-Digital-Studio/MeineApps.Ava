using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FinanzRechner.Models;

namespace FinanzRechner.Converters;

/// <summary>
/// Converter for Transaction Type Toggle-Buttons.
/// Returns SelectedColor brush when TransactionType == TargetType, otherwise UnselectedColor.
/// Uses TryFindResource for theme support.
/// Note: Because this converter has settable properties, it cannot use the singleton Instance pattern.
/// Create instances in XAML resources instead.
/// </summary>
public class TransactionTypeToggleColorConverter : IValueConverter
{
    private static readonly IBrush ExpenseToggleBrush = ExpenseToggleBrush;
    private static readonly IBrush IncomeToggleBrush = IncomeToggleBrush;

    public TransactionType TargetType { get; set; }
    public string SelectedColorKey { get; set; } = "ExpenseColor";
    public string UnselectedColorKey { get; set; } = "ButtonBackgroundColor";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TransactionType currentType)
        {
            var isSelected = currentType == TargetType;
            var resourceKey = isSelected ? SelectedColorKey : UnselectedColorKey;

            // Get brush from application resources
            var app = Application.Current;
            if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var resourceValue) && resourceValue is IBrush brush)
            {
                return brush;
            }

            // Fallback colors
            return isSelected
                ? (TargetType == TransactionType.Expense
                    ? ExpenseToggleBrush
                    : IncomeToggleBrush)
                : Brushes.Transparent;
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
