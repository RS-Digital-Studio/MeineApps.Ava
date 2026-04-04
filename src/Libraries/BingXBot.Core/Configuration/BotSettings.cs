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
    public bool ShowBtcTicker { get; set; } = true;

    /// <summary>Startkapital für Paper-Trading in USDT.</summary>
    public decimal PaperInitialBalance { get; set; } = 10_000m;

    // === ATI-Konfiguration ===
    /// <summary>Mindestanzahl Trades bevor ATI-Gewichtungen aktiv werden (Cold-Start-Schutz).</summary>
    public int AtiMinTradesBeforeLearning { get; set; } = 20;
    /// <summary>Intervall in Minuten für automatische ATI-State-Persistierung (0 = deaktiviert).</summary>
    public int AtiAutoSaveIntervalMinutes { get; set; } = 15;

    // === Benachrichtigungen ===
    /// <summary>Desktop-Benachrichtigungen bei Trade-Eröffnung/-Schließung.</summary>
    public bool EnableDesktopNotifications { get; set; } = true;

    // === Funding-Rate für Simulation ===
    /// <summary>Simulierte Funding-Rate pro 8h für Paper/Backtest (in %, z.B. 0.01 = 0.01%).</summary>
    public decimal SimulatedFundingRatePercent { get; set; } = 0.01m;
}
