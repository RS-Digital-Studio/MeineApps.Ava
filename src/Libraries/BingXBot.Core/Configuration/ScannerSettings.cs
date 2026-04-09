using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class ScannerSettings
{
    public decimal MinVolume24h { get; set; } = 20_000_000m;
    /// <summary>Min. 24h-Preisänderung in %. 0.5% = fast alle relevanten Paare. Zu hoch = ruhige Märkte werden ignoriert.</summary>
    public decimal MinPriceChange { get; set; } = 0.5m;
    public TimeFrame ScanTimeFrame { get; set; } = TimeFrame.H4;
    public List<string> Blacklist { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();
    /// <summary>Max. Kandidaten die pro Scan evaluiert werden. 50 = guter Kompromiss aus Abdeckung und API-Last.</summary>
    public int MaxResults { get; set; } = 50;
    public ScanMode Mode { get; set; } = ScanMode.Momentum;

    /// <summary>
    /// Nur die Top-N Coins nach 24h-Volumen analysieren.
    /// Auf Futures-Börsen korreliert 24h-Volume stark mit Market Cap:
    /// Top-100 nach Volume auf BingX = effektiv die großen, liquiden Coins.
    /// Kleine/Meme-Coins mit niedrigem Volume werden ausgefiltert.
    /// </summary>
    public bool OnlyTopByVolume { get; set; } = true;

    /// <summary>Anzahl der Top-Coins nach Volume die analysiert werden. Default 100 = ~Top-100 Market Cap.</summary>
    public int TopCoinsCount { get; set; } = 100;

    /// <summary>
    /// Scan-Intervall in Sekunden. Bei H4 trotzdem alle 5min scannen:
    /// H4-Candles ändern sich zwar nur alle 4h, aber der Ticker-Preis und
    /// Volumen ändern sich ständig → neue Kandidaten können auftauchen.
    /// Außerdem nutzt CryptoTrendPro interne M15-Checks für Entry-Timing.
    /// </summary>
    public int ScanIntervalSeconds => ScanTimeFrame switch
    {
        TimeFrame.M1 => 30,
        TimeFrame.M3 => 45,
        TimeFrame.M5 => 60,
        TimeFrame.M15 => 90,
        TimeFrame.M30 => 120,
        TimeFrame.H1 => 180,
        TimeFrame.H2 => 300,
        TimeFrame.H4 => 300,  // 5min: Ticker-Preis + Kandidaten-Check, nicht nur Candles
        TimeFrame.H6 => 600,
        TimeFrame.H12 => 900,
        TimeFrame.D1 => 1800,
        _ => 300
    };

    /// <summary>Ob M15-Candles zusätzlich für Entry-Timing geladen werden (bei H4/H1 Strategien).</summary>
    public bool UseM15EntryTiming { get; set; } = true;

    /// <summary>TradFi-Assets aktivieren (Gold, Nasdaq, Forex, Aktien). Default: true (alle Märkte).</summary>
    public bool EnableTradFi { get; set; } = true;

    /// <summary>Welche TradFi-Kategorien aktiviert sind. Default: Alle 5 Märkte.</summary>
    public HashSet<MarketCategory> EnabledCategories { get; set; } = new()
    {
        MarketCategory.Crypto, MarketCategory.Commodity, MarketCategory.Index,
        MarketCategory.Forex, MarketCategory.Stock
    };

    /// <summary>Min. 24h-Volume für TradFi-Assets (niedriger als Krypto, da weniger Symbole).</summary>
    public decimal MinVolume24hTradFi { get; set; } = 1_000_000m;

    /// <summary>
    /// Wird zur Laufzeit gesetzt: True wenn der BingX-Account im Hedge-Modus (Dual-Side) ist.
    /// TradFi-Symbole brauchen Hedge-Modus (BingX Error 101414 bei One-Way-Mode).
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsHedgeModeActive { get; set; }

    /// <summary>
    /// Higher-Timeframe für Trend-Konfirmation. Wird automatisch aus dem ScanTimeFrame abgeleitet:
    /// M15→H1, H1→H4, H4→D1. Kann vom Preset überschrieben werden.
    /// </summary>
    public TimeFrame HtfTimeFrame => ScanTimeFrame switch
    {
        TimeFrame.M1 or TimeFrame.M3 or TimeFrame.M5 => TimeFrame.M15,
        TimeFrame.M15 or TimeFrame.M30 => TimeFrame.H1,
        TimeFrame.H1 or TimeFrame.H2 => TimeFrame.H4,
        TimeFrame.H4 or TimeFrame.H6 => TimeFrame.D1,
        _ => TimeFrame.D1
    };
}
