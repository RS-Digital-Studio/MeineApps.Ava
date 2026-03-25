namespace HandwerkerImperium.Helpers;

/// <summary>
/// Einheitliche Geldbetrags-Formatierung für die gesamte App.
/// Unterstützt negative Werte (Netto-Verlust) und Werte bis Octillion (10^27).
/// decimal max ≈ 7.9 × 10^28, reicht für alle Prestige-Stufen.
/// </summary>
public static class MoneyFormatter
{
    // Schwellenwerte für Idle-Game-Suffixe (decimal max ≈ 7.9 × 10^28)
    private const decimal Oc = 1_000_000_000_000_000_000_000_000_000m; // 10^27 Octillion
    private const decimal Sp = 1_000_000_000_000_000_000_000_000m;     // 10^24 Septillion
    private const decimal Sx = 1_000_000_000_000_000_000_000m;         // 10^21 Sextillion
    private const decimal Qi = 1_000_000_000_000_000_000m;             // 10^18 Quintillion
    private const decimal Qa = 1_000_000_000_000_000m;                 // 10^15 Quadrillion
    private const decimal T  = 1_000_000_000_000m;                     // 10^12 Trillion
    private const decimal B  = 1_000_000_000m;                         // 10^9  Billion
    private const decimal M  = 1_000_000m;                             // 10^6  Million
    private const decimal K  = 1_000m;                                 // 10^3  Tausend

    /// <summary>
    /// Formatiert Geldbetrag für Anzeige (kompakt).
    /// </summary>
    public static string Format(decimal amount, int decimals = 1)
    {
        var format = GetFormat(decimals);
        var (abs, prefix) = SplitSign(amount);

        return abs switch
        {
            >= Oc => $"{prefix}{(abs / Oc).ToString(format)}Oc \u20AC",
            >= Sp => $"{prefix}{(abs / Sp).ToString(format)}Sp \u20AC",
            >= Sx => $"{prefix}{(abs / Sx).ToString(format)}Sx \u20AC",
            >= Qi => $"{prefix}{(abs / Qi).ToString(format)}Qi \u20AC",
            >= Qa => $"{prefix}{(abs / Qa).ToString(format)}Qa \u20AC",
            >= T  => $"{prefix}{(abs / T).ToString(format)}T \u20AC",
            >= B  => $"{prefix}{(abs / B).ToString(format)}B \u20AC",
            >= M  => $"{prefix}{(abs / M).ToString(format)}M \u20AC",
            >= K  => $"{prefix}{(abs / K).ToString(format)}K \u20AC",
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
            >= Oc => $"{prefix}{(abs / Oc).ToString(format)}Oc \u20AC/s",
            >= Sp => $"{prefix}{(abs / Sp).ToString(format)}Sp \u20AC/s",
            >= Sx => $"{prefix}{(abs / Sx).ToString(format)}Sx \u20AC/s",
            >= Qi => $"{prefix}{(abs / Qi).ToString(format)}Qi \u20AC/s",
            >= Qa => $"{prefix}{(abs / Qa).ToString(format)}Qa \u20AC/s",
            >= T  => $"{prefix}{(abs / T).ToString(format)}T \u20AC/s",
            >= B  => $"{prefix}{(abs / B).ToString(format)}B \u20AC/s",
            >= M  => $"{prefix}{(abs / M).ToString(format)}M \u20AC/s",
            >= K  => $"{prefix}{(abs / K).ToString(format)}K \u20AC/s",
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
            >= Oc => $"{prefix}{(abs / Oc).ToString(format)}Oc \u20AC/h",
            >= Sp => $"{prefix}{(abs / Sp).ToString(format)}Sp \u20AC/h",
            >= Sx => $"{prefix}{(abs / Sx).ToString(format)}Sx \u20AC/h",
            >= Qi => $"{prefix}{(abs / Qi).ToString(format)}Qi \u20AC/h",
            >= Qa => $"{prefix}{(abs / Qa).ToString(format)}Qa \u20AC/h",
            >= T  => $"{prefix}{(abs / T).ToString(format)}T \u20AC/h",
            >= B  => $"{prefix}{(abs / B).ToString(format)}B \u20AC/h",
            >= M  => $"{prefix}{(abs / M).ToString(format)}M \u20AC/h",
            >= K  => $"{prefix}{(abs / K).ToString(format)}K \u20AC/h",
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
            >= Oc => $"{prefix}{abs / Oc:F1}Oc \u20AC",
            >= Sp => $"{prefix}{abs / Sp:F1}Sp \u20AC",
            >= Sx => $"{prefix}{abs / Sx:F1}Sx \u20AC",
            >= Qi => $"{prefix}{abs / Qi:F1}Qi \u20AC",
            >= Qa => $"{prefix}{abs / Qa:F1}Qa \u20AC",
            >= T  => $"{prefix}{abs / T:F1}T \u20AC",
            >= B  => $"{prefix}{abs / B:F1}B \u20AC",
            >= M  => $"{prefix}{abs / M:F1}M \u20AC",
            >= K  => $"{prefix}{abs / K:F1}K \u20AC",
            _ => $"{prefix}{abs:N0} \u20AC"
        };
    }

    /// <summary>
    /// Kompakte Zahl OHNE €-Suffix (für Scores, XP, Nicht-Geld-Werte).
    /// </summary>
    public static string FormatNumber(decimal amount)
    {
        var (abs, prefix) = SplitSign(amount);

        return abs switch
        {
            >= Oc => $"{prefix}{abs / Oc:F1}Oc",
            >= Sp => $"{prefix}{abs / Sp:F1}Sp",
            >= Sx => $"{prefix}{abs / Sx:F1}Sx",
            >= Qi => $"{prefix}{abs / Qi:F1}Qi",
            >= Qa => $"{prefix}{abs / Qa:F1}Qa",
            >= T  => $"{prefix}{abs / T:F1}T",
            >= B  => $"{prefix}{abs / B:F1}B",
            >= M  => $"{prefix}{abs / M:F1}M",
            >= K  => $"{prefix}{abs / K:F1}K",
            _ => $"{prefix}{abs:N0}"
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
