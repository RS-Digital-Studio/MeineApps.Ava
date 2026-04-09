using BingXBot.Core.Enums;

namespace BingXBot.Core.Helpers;

/// <summary>
/// Klassifiziert BingX-Symbole nach Markt-Kategorie anhand des Symbol-Prefix.
/// TradFi-Symbole nutzen NC-Prefix: NCCO (Commodities), NCSI (Indices), NCFX (Forex), NCSK (Stocks).
/// Alle anderen Symbole sind Krypto-Perps.
/// </summary>
public static class SymbolClassifier
{
    /// <summary>Bestimmt die Marktkategorie anhand des Symbol-Prefix.</summary>
    public static MarketCategory Classify(string symbol) => symbol switch
    {
        _ when symbol.StartsWith("NCCO") => MarketCategory.Commodity,
        _ when symbol.StartsWith("NCSI") => MarketCategory.Index,
        _ when symbol.StartsWith("NCFX") => MarketCategory.Forex,
        _ when symbol.StartsWith("NCSK") => MarketCategory.Stock,
        _ => MarketCategory.Crypto
    };

    /// <summary>True wenn das Symbol ein TradFi-Asset ist (Commodity, Index, Forex, Stock).</summary>
    public static bool IsTradFi(string symbol) => symbol.StartsWith("NC");

    /// <summary>True wenn das Symbol eine 24/7-Variante ist (z.B. NCCO724OILWTI2USD-USDT).</summary>
    public static bool Is24x7(string symbol) => symbol.Contains("724");

    /// <summary>
    /// Prüft ob API-Trading für dieses Symbol erlaubt ist.
    /// Manche TradFi-Symbole haben apiStateOpen=false auf BingX.
    /// </summary>
    public static bool IsApiTradeable(string symbol)
    {
        // 7x24-Öl/Index-Varianten: API aktuell gesperrt
        if (symbol.Contains("724")) return false;
        // Exotische Forex-Paare: API gesperrt
        if (symbol.Contains("2HKD-") || symbol.Contains("2SGD-")) return false;
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

    /// <summary>Gibt einen lesbaren Symbol-Namen zurück (z.B. "Gold" statt "NCCOGOLD2USD-USDT").</summary>
    public static string GetDisplayName(string symbol)
    {
        if (!IsTradFi(symbol)) return symbol;

        // TradFi-Symbole: Prefix entfernen, 2USD-USDT entfernen
        var name = symbol;

        // Prefix entfernen (NCCO, NCSI, NCFX, NCSK, NCCO1, NCSI1)
        if (name.StartsWith("NCCO1")) name = name[5..];
        else if (name.StartsWith("NCCO")) name = name[4..];
        else if (name.StartsWith("NCSI")) name = name[4..];
        else if (name.StartsWith("NCFX")) name = name[4..];
        else if (name.StartsWith("NCSK")) name = name[4..];

        // 2USD-USDT Suffix entfernen
        var idx = name.IndexOf("2USD-USDT");
        if (idx > 0) name = name[..idx];

        // 2JPY-USDT etc. für Forex
        idx = name.IndexOf("2JPY-");
        if (idx > 0) name = name[..idx] + "/JPY";
        idx = name.IndexOf("2GBP-");
        if (idx > 0) name = name[..idx] + "/GBP";
        idx = name.IndexOf("2EUR-");
        if (idx > 0) name = name[..idx] + "/EUR";

        return name.Length > 0 ? name : symbol;
    }
}
