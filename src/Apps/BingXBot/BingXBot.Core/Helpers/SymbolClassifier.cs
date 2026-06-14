using BingXBot.Core.Enums;

namespace BingXBot.Core.Helpers;

/// <summary>
/// Klassifiziert BingX-Symbole nach Markt-Kategorie anhand des Symbol-Prefix.
/// TradFi-Symbole nutzen NC-Prefix: NCCO (Commodities), NCSI (Indices), NCFX (Forex), NCSK (Stocks).
/// Alle anderen Symbole sind Krypto-Perps.
/// </summary>
public static class SymbolClassifier
{
    // WICHTIG: BingX-API liefert Symbole in gemischter Schreibweise (z.B. "Ncco1Oilwti2USD-USDT").
    // Alle Prefix-/Contains-Checks MUESSEN case-insensitive sein, sonst werden TradFi-Symbole
    // als Krypto klassifiziert und landen im Krypto-Filter statt im TradFi-Filter.
    private const StringComparison IgnoreCase = StringComparison.OrdinalIgnoreCase;

    /// <summary>Bestimmt die Marktkategorie anhand des Symbol-Prefix (case-insensitive).</summary>
    public static MarketCategory Classify(string symbol) => symbol switch
    {
        _ when symbol.StartsWith("NCCO", IgnoreCase) => MarketCategory.Commodity,
        _ when symbol.StartsWith("NCSI", IgnoreCase) => MarketCategory.Index,
        _ when symbol.StartsWith("NCFX", IgnoreCase) => MarketCategory.Forex,
        _ when symbol.StartsWith("NCSK", IgnoreCase) => MarketCategory.Stock,
        _ => MarketCategory.Crypto
    };

    /// <summary>True wenn das Symbol ein TradFi-Asset ist (Commodity, Index, Forex, Stock).</summary>
    public static bool IsTradFi(string symbol) => symbol.StartsWith("NC", IgnoreCase);

    /// <summary>True wenn das Symbol eine 24/7-Variante ist (z.B. Ncco724OilWti2USD-USDT).</summary>
    public static bool Is24x7(string symbol) => symbol.Contains("724", IgnoreCase);

    /// <summary>
    /// Prüft ob API-Trading für dieses Symbol erlaubt ist.
    /// Manche TradFi-Symbole haben apiStateOpen=false auf BingX.
    /// Stand 13.04.2026: 7x24-Varianten (NCCO724OILWTI, NCSI724SPX500, etc.) sind seit BingX-Release
    /// wieder API-tradebar — der Filter ist entfernt. Falls einzelne 724-Symbole abgelehnt werden
    /// (Error 101414 "symbol disabled"), werden sie per Blacklist im ScannerSettings ausgeschlossen.
    /// </summary>
    public static bool IsApiTradeable(string symbol)
    {
        // Exotische Forex-Paare: API gesperrt (2HKD, 2SGD). Case-insensitive wegen Mixed-Case-Symbolen.
        if (symbol.Contains("2HKD-", IgnoreCase) || symbol.Contains("2SGD-", IgnoreCase)) return false;
        return true;
    }

    /// <summary>Gibt einen lesbaren deutschen Kategorie-Namen zurück.</summary>
    public static string GetCategoryDisplayName(MarketCategory category) => category switch
    {
        MarketCategory.Crypto => "Krypto",
        MarketCategory.Commodity => "Rohstoffe",
        MarketCategory.Index => "Indices",
        MarketCategory.Forex => "Forex",
        MarketCategory.Stock => "Aktien",
        _ => "Unbekannt"
    };

    /// <summary>Gibt einen lesbaren Symbol-Namen zurück (z.B. "Gold" statt "NCCOGOLD2USD-USDT").
    /// Case-insensitive wegen Mixed-Case-Symbolen von BingX.</summary>
    public static string GetDisplayName(string symbol)
    {
        if (!IsTradFi(symbol)) return symbol;

        // TradFi-Symbole: Prefix entfernen, 2USD-USDT entfernen
        var name = symbol;

        // Prefix entfernen (NCCO, NCSI, NCFX, NCSK, NCCO1, NCSI1) — reihenfolge: längster Prefix zuerst
        if (name.StartsWith("NCCO1", IgnoreCase)) name = name[5..];
        else if (name.StartsWith("NCSI1", IgnoreCase)) name = name[5..];
        else if (name.StartsWith("NCCO", IgnoreCase)) name = name[4..];
        else if (name.StartsWith("NCSI", IgnoreCase)) name = name[4..];
        else if (name.StartsWith("NCFX", IgnoreCase)) name = name[4..];
        else if (name.StartsWith("NCSK", IgnoreCase)) name = name[4..];

        // 2USD-USDT Suffix entfernen
        var idx = name.IndexOf("2USD-USDT", IgnoreCase);
        if (idx > 0) name = name[..idx];

        // 2JPY-USDT etc. für Forex
        idx = name.IndexOf("2JPY-", IgnoreCase);
        if (idx > 0) name = name[..idx] + "/JPY";
        idx = name.IndexOf("2GBP-", IgnoreCase);
        if (idx > 0) name = name[..idx] + "/GBP";
        idx = name.IndexOf("2EUR-", IgnoreCase);
        if (idx > 0) name = name[..idx] + "/EUR";

        return name.Length > 0 ? name : symbol;
    }
}
