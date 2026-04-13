using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

public class BotSettings
{
    public RiskSettings Risk { get; set; } = new();
    public ScannerSettings Scanner { get; set; } = new();
    public BacktestSettings Backtest { get; set; } = new();
    public TradingMode LastMode { get; set; } = TradingMode.Paper;
    public string? LastStrategyName { get; set; }
    /// <summary>Letzter Trading-Modus (Scalping/DayTrading/Swing/Custom).</summary>
    public TradingModePreset LastTradingModePreset { get; set; } = TradingModePreset.Swing;
    public Dictionary<string, string> StrategyParameters { get; set; } = new();
    public bool ShowBtcTicker { get; set; } = true;

    /// <summary>Startkapital für Paper-Trading in USDT.</summary>
    public decimal PaperInitialBalance { get; set; } = 10_000m;

    // === Benachrichtigungen ===
    /// <summary>Desktop-Benachrichtigungen bei Trade-Eröffnung/-Schließung.</summary>
    public bool EnableDesktopNotifications { get; set; } = true;

    // === Funding-Rate für Simulation ===
    /// <summary>Simulierte Funding-Rate pro 8h für Paper/Backtest (in %, z.B. 0.01 = 0.01%).</summary>
    public decimal SimulatedFundingRatePercent { get; set; } = 0.01m;
}
