namespace HandwerkerImperium.Helpers;

/// <summary>
/// Centralized money formatting for consistent display across the app.
/// </summary>
public static class MoneyFormatter
{
    /// <summary>
    /// Formats money amount for display (compact format).
    /// Uses consistent thresholds: B >= 1B, M >= 1M, K >= 1K.
    /// </summary>
    /// <param name="amount">The amount to format</param>
    /// <param name="decimals">Number of decimal places (default: 1)</param>
    /// <returns>Formatted string like "1.5K EUR" or "500 EUR"</returns>
    public static string Format(decimal amount, int decimals = 1)
    {
        var format = decimals switch
        {
            0 => "F0",
            1 => "F1",
            2 => "F2",
            _ => $"F{decimals}"
        };

        return amount switch
        {
            >= 1_000_000_000_000 => $"{(amount / 1_000_000_000_000).ToString(format)}T \u20AC",
            >= 1_000_000_000 => $"{(amount / 1_000_000_000).ToString(format)}B \u20AC",
            >= 1_000_000 => $"{(amount / 1_000_000).ToString(format)}M \u20AC",
            >= 1_000 => $"{(amount / 1_000).ToString(format)}K \u20AC",
            _ => $"{amount:N0} \u20AC"
        };
    }

    /// <summary>
    /// Formats money amount with per-second suffix.
    /// </summary>
    public static string FormatPerSecond(decimal amount, int decimals = 2)
    {
        var format = decimals switch
        {
            0 => "F0",
            1 => "F1",
            2 => "F2",
            _ => $"F{decimals}"
        };

        return amount switch
        {
            >= 1_000_000_000_000 => $"{(amount / 1_000_000_000_000).ToString(format)}T \u20AC/s",
            >= 1_000_000_000 => $"{(amount / 1_000_000_000).ToString(format)}B \u20AC/s",
            >= 1_000_000 => $"{(amount / 1_000_000).ToString(format)}M \u20AC/s",
            >= 1_000 => $"{(amount / 1_000).ToString(format)}K \u20AC/s",
            _ => $"{amount.ToString(format)} \u20AC/s"
        };
    }

    /// <summary>
    /// Formatiert Geldbetrag mit /h-Suffix (pro Stunde).
    /// </summary>
    public static string FormatPerHour(decimal amount, int decimals = 0)
    {
        var format = decimals switch
        {
            0 => "F0",
            1 => "F1",
            2 => "F2",
            _ => $"F{decimals}"
        };

        return amount switch
        {
            >= 1_000_000_000_000 => $"{(amount / 1_000_000_000_000).ToString(format)}T \u20AC/h",
            >= 1_000_000_000 => $"{(amount / 1_000_000_000).ToString(format)}B \u20AC/h",
            >= 1_000_000 => $"{(amount / 1_000_000).ToString(format)}M \u20AC/h",
            >= 1_000 => $"{(amount / 1_000).ToString(format)}K \u20AC/h",
            _ => $"{amount.ToString(format)} \u20AC/h"
        };
    }

    /// <summary>
    /// Formats money for UI display (no decimals for small amounts).
    /// </summary>
    public static string FormatCompact(decimal amount)
    {
        return amount switch
        {
            >= 1_000_000_000_000 => $"{amount / 1_000_000_000_000:F1}T \u20AC",
            >= 1_000_000_000 => $"{amount / 1_000_000_000:F1}B \u20AC",
            >= 1_000_000 => $"{amount / 1_000_000:F1}M \u20AC",
            >= 1_000 => $"{amount / 1_000:F1}K \u20AC",
            _ => $"{amount:N0} \u20AC"
        };
    }
}
