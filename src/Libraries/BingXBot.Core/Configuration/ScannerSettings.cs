using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class ScannerSettings
{
    public decimal MinVolume24h { get; set; } = 10_000_000m;
    public decimal MinPriceChange { get; set; } = 1.0m;
    public TimeFrame ScanTimeFrame { get; set; } = TimeFrame.H1;
    public List<string> Blacklist { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();
    public int MaxResults { get; set; } = 10;
    public ScanMode Mode { get; set; } = ScanMode.Momentum;
}
