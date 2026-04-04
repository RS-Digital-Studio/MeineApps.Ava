using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class ScannerSettings
{
    public decimal MinVolume24h { get; set; } = 50_000_000m;
    public decimal MinPriceChange { get; set; } = 2.0m;
    public TimeFrame ScanTimeFrame { get; set; } = TimeFrame.H4;
    public List<string> Blacklist { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();
    public int MaxResults { get; set; } = 5;
    public ScanMode Mode { get; set; } = ScanMode.Momentum;

    /// <summary>
    /// Scan-Intervall in Sekunden. Wird automatisch an den Timeframe angepasst.
    /// H4 = 900s (15min), H1 = 300s (5min), M15 = 60s.
    /// </summary>
    public int ScanIntervalSeconds => ScanTimeFrame switch
    {
        TimeFrame.M1 => 30,
        TimeFrame.M3 => 60,
        TimeFrame.M5 => 60,
        TimeFrame.M15 => 120,
        TimeFrame.M30 => 180,
        TimeFrame.H1 => 300,
        TimeFrame.H2 => 600,
        TimeFrame.H4 => 900,
        TimeFrame.H6 => 1800,
        TimeFrame.H12 => 3600,
        TimeFrame.D1 => 7200,
        _ => 900
    };
}
