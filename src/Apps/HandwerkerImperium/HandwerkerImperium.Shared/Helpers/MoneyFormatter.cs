namespace HandwerkerImperium.Helpers;

/// <summary>
/// Einheitliche Geldbetrags-Formatierung für die gesamte App.
/// Unterstützt negative Werte (Netto-Verlust) und Werte bis Trillion.
/// Schwellen: T >= 1T, B >= 1B, M >= 1M, K >= 1K.
/// </summary>
public static class MoneyFormatter
{
    /// <summary>
    /// Formatiert Geldbetrag für Anzeige (kompakt).
    /// </summary>
    public static string Format(decimal amount, int decimals = 1)
    {
        var format = GetFormat(decimals);
        var (abs, prefix) = SplitSign(amount);

        return abs switch
        {
            >= 1_000_000_000_000 => $"{prefix}{(abs / 1_000_000_000_000).ToString(format)}T \u20AC",
            >= 1_000_000_000 => $"{prefix}{(abs / 1_000_000_000).ToString(format)}B \u20AC",
            >= 1_000_000 => $"{prefix}{(abs / 1_000_000).ToString(format)}M \u20AC",
            >= 1_000 => $"{prefix}{(abs / 1_000).ToString(format)}K \u20AC",
            _ => $"{prefix}{abs:N0} \u20AC"
        };
    }

    /// <summary>
    /// Formatiert Geldbetrag mit /s-Suffix (pro Sekunde).
    /// </summary>
    public static string FormatPerSecond(decimal amount, int decimals = 2)
    {
        var format = GetFormat(decimals);
        var (abs, prefix) = SplitSign(amount);

        return abs switch
        {
            >= 1_000_000_000_000 => $"{prefix}{(abs / 1_000_000_000_000).ToString(format)}T \u20AC/s",
            >= 1_000_000_000 => $"{prefix}{(abs / 1_000_000_000).ToString(format)}B \u20AC/s",
            >= 1_000_000 => $"{prefix}{(abs / 1_000_000).ToString(format)}M \u20AC/s",
            >= 1_000 => $"{prefix}{(abs / 1_000).ToString(format)}K \u20AC/s",
            _ => $"{prefix}{abs.ToString(format)} \u20AC/s"
        };
    }

    /// <summary>
    /// Formatiert Geldbetrag mit /h-Suffix (pro Stunde).
    /// </summary>
    public static string FormatPerHour(decimal amount, int decimals = 0)
    {
        var format = GetFormat(decimals);
        var (abs, prefix) = SplitSign(amount);

        return abs switch
        {
            >= 1_000_000_000_000 => $"{prefix}{(abs / 1_000_000_000_000).ToString(format)}T \u20AC/h",
            >= 1_000_000_000 => $"{prefix}{(abs / 1_000_000_000).ToString(format)}B \u20AC/h",
            >= 1_000_000 => $"{prefix}{(abs / 1_000_000).ToString(format)}M \u20AC/h",
            >= 1_000 => $"{prefix}{(abs / 1_000).ToString(format)}K \u20AC/h",
            _ => $"{prefix}{abs.ToString(format)} \u20AC/h"
        };
    }

    /// <summary>
    /// Kompakte Formatierung für UI (1 Dezimalstelle für große Werte, keine für kleine).
    /// </summary>
    public static string FormatCompact(decimal amount)
    {
        var (abs, prefix) = SplitSign(amount);

        return abs switch
        {
            >= 1_000_000_000_000 => $"{prefix}{abs / 1_000_000_000_000:F1}T \u20AC",
            >= 1_000_000_000 => $"{prefix}{abs / 1_000_000_000:F1}B \u20AC",
            >= 1_000_000 => $"{prefix}{abs / 1_000_000:F1}M \u20AC",
            >= 1_000 => $"{prefix}{abs / 1_000:F1}K \u20AC",
            _ => $"{prefix}{abs:N0} \u20AC"
        };
    }

    /// <summary>
    /// Zerlegt Betrag in Absolutwert und Vorzeichen-Prefix.
    /// Verwendet echtes Minus-Zeichen (U+2212) für bessere Lesbarkeit.
    /// </summary>
    private static (decimal abs, string prefix) SplitSign(decimal amount)
    {
        if (amount < 0)
            return (Math.Abs(amount), "\u2212");
        return (amount, "");
    }

    private static string GetFormat(int decimals) => decimals switch
    {
        0 => "F0",
        1 => "F1",
        2 => "F2",
        _ => $"F{decimals}"
    };
}
