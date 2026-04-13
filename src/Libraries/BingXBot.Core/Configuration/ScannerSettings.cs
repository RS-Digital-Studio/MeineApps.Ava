using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class ScannerSettings
{
    public decimal MinVolume24h { get; set; } = 1_000_000m;
    /// <summary>Min. 24h-Preisänderung in %. 0.1% zeigt auch Stabilisierungsphasen (SK).</summary>
    public decimal MinPriceChange { get; set; } = 0.1m;
    /// <summary>Scanner-Haupt-Timeframe (Navigator-Chart). SK-Buch: H4.</summary>
    public TimeFrame ScanTimeFrame { get; set; } = TimeFrame.H4;
    public List<string> Blacklist { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();
    /// <summary>Max. Kandidaten pro Scan. SK braucht breites Screening.</summary>
    public int MaxResults { get; set; } = 100;
    /// <summary>SK = Mean-Reversion (nicht Momentum).</summary>
    public ScanMode Mode { get; set; } = ScanMode.Reversal;

    /// <summary>Top-N Coins nach Volume/Market-Cap.</summary>
    public bool OnlyTopByVolume { get; set; } = true;
    public int TopCoinsCount { get; set; } = 100;

    /// <summary>Scan-Intervall in Sekunden (abhängig vom Scan-Timeframe).</summary>
    public int ScanIntervalSeconds => ScanTimeFrame switch
    {
        TimeFrame.M1 => 30,
        TimeFrame.M3 => 45,
        TimeFrame.M5 => 60,
        TimeFrame.M15 => 90,
        TimeFrame.M30 => 120,
        TimeFrame.H1 => 180,
        TimeFrame.H2 => 300,
        TimeFrame.H4 => 60,   // SK braucht schnelle M30-Trigger-Reaktion
        TimeFrame.H6 => 600,
        TimeFrame.H12 => 900,
        TimeFrame.D1 => 1800,
        _ => 300
    };

    /// <summary>TradFi-Assets aktivieren (Gold, Nasdaq, Forex, Aktien).</summary>
    public bool EnableTradFi { get; set; } = true;

    /// <summary>Welche TradFi-Kategorien aktiviert sind.</summary>
    public HashSet<MarketCategory> EnabledCategories { get; set; } = new()
    {
        MarketCategory.Crypto, MarketCategory.Commodity, MarketCategory.Index,
        MarketCategory.Forex, MarketCategory.Stock
    };

    /// <summary>Min. 24h-Volume für TradFi-Assets.</summary>
    public decimal MinVolume24hTradFi { get; set; } = 1_000_000m;
    public decimal MinPriceChangeTradFi { get; set; } = 0.1m;

    /// <summary>Wird zur Laufzeit gesetzt: True wenn BingX-Account im Hedge-Modus.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsHedgeModeActive { get; set; }

    /// <summary>Higher-Timeframe (Filter-Chart). SK-Buch: H1 bei H4-Scanner.</summary>
    public TimeFrame HtfTimeFrame => ScanTimeFrame switch
    {
        TimeFrame.M1 or TimeFrame.M3 or TimeFrame.M5 => TimeFrame.M15,
        TimeFrame.M15 or TimeFrame.M30 => TimeFrame.H1,
        TimeFrame.H1 or TimeFrame.H2 => TimeFrame.H4,
        TimeFrame.H4 or TimeFrame.H6 => TimeFrame.D1,
        _ => TimeFrame.D1
    };
}
