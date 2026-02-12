using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FinanzRechner.Models;

namespace FinanzRechner.Converters;

/// <summary>
/// Universeller Converter von TransactionType zu verschiedenen Werttypen.
/// Unterstützt IBrush, double, string, FontWeight, bool.
/// Parameter-Format: "0=Value1,1=Value2" wobei 0=Expense, 1=Income
/// </summary>
public class TransactionTypeToValueConverter : IValueConverter
{
    public static readonly TransactionTypeToValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TransactionType transactionType || parameter is not string paramString)
        {
            return GetDefaultValue(targetType);
        }

        var isExpense = transactionType == TransactionType.Expense;
        var parts = paramString.Split(',');

        foreach (var part in parts)
        {
            var keyValue = part.Split('=');
            if (keyValue.Length != 2) continue;

            var key = keyValue[0].Trim();
            var val = keyValue[1].Trim();

            // Prüfen ob dieser Teil zum aktuellen TransactionType passt
            bool matches = (key == "0" && isExpense) || (key == "1" && !isExpense);

            if (matches)
            {
                return ConvertValue(val, targetType);
            }
        }

        return GetDefaultValue(targetType);
    }

    private static object? ConvertValue(string value, Type targetType)
    {
        try
        {
            if (targetType == typeof(IBrush) || targetType == typeof(ISolidColorBrush) || targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
            {
                return new SolidColorBrush(Color.Parse(value));
            }
            else if (targetType == typeof(double))
            {
                return double.Parse(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(FontWeight))
            {
                return Enum.Parse<FontWeight>(value, ignoreCase: true);
            }
            else if (targetType == typeof(string))
            {
                return value;
            }
            else if (targetType == typeof(bool))
            {
                return bool.Parse(value);
            }
        }
        catch
        {
            // Weiter zum Standardwert
        }

        return GetDefaultValue(targetType);
    }

    private static object? GetDefaultValue(Type targetType)
    {
        if (targetType == typeof(IBrush) || targetType == typeof(ISolidColorBrush) || targetType == typeof(Brush) || targetType == typeof(SolidColorBrush))
            return Brushes.Transparent;
        else if (targetType == typeof(double))
            return 1.0;
        else if (targetType == typeof(FontWeight))
            return FontWeight.Normal;
        else if (targetType == typeof(string))
            return string.Empty;
        else if (targetType == typeof(bool))
            return false;

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
