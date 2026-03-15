using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class BotSettings
{
    public RiskSettings Risk { get; set; } = new();
    public ScannerSettings Scanner { get; set; } = new();
    public BacktestSettings Backtest { get; set; } = new();
    public TradingMode LastMode { get; set; } = TradingMode.Paper;
    public string? LastStrategyName { get; set; }
    public Dictionary<string, string> StrategyParameters { get; set; } = new();
}
