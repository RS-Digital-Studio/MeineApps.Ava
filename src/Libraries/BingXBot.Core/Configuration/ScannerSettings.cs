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
}
