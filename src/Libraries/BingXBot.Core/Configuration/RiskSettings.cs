namespace BingXBot.Core.Configuration;

public class RiskSettings
{
    public decimal MaxPositionSizePercent { get; set; } = 2m;
    public decimal MaxDailyDrawdownPercent { get; set; } = 5m;
    public decimal MaxTotalDrawdownPercent { get; set; } = 15m;
    public int MaxOpenPositions { get; set; } = 3;
    public int MaxOpenPositionsPerSymbol { get; set; } = 1;
    public decimal MaxLeverage { get; set; } = 10m;
    public bool CheckCorrelation { get; set; } = true;
    public decimal MaxCorrelation { get; set; } = 0.7m;
    public bool EnableTrailingStop { get; set; } = true;
    public decimal TrailingStopPercent { get; set; } = 1.5m;
}
