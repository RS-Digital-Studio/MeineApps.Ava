namespace BingXBot.Core.Configuration;

/// <summary>
/// Backtest-Settings. Nach Buch-Refactoring: TP1+TP2 als 50/50-Staffelung (Buch Zielbereich 161.8-200%),
/// kein Trailing, kein Smart-Breakeven (SK hat eigene BE-Regel Workflow 4.2).
/// </summary>
public class BacktestSettings
{
    public decimal InitialBalance { get; set; } = 1000m;
    public decimal MakerFee { get; set; } = 0.0002m;
    public decimal TakerFee { get; set; } = 0.0005m;
    public decimal SlippagePercent { get; set; } = 0.05m;
    public bool SimulateFundingRate { get; set; } = true;
    /// <summary>Simulierte Funding-Rate pro 8h-Intervall in % (z.B. 0.01 = 0.01%).</summary>
    public decimal SimulatedFundingRatePercent { get; set; } = 0.01m;

    // === Realistisches Markt-Modell ===
    /// <summary>Dynamische Slippage basierend auf ATR/Volumen statt fixem Prozentsatz.</summary>
    public bool UseDynamicSlippage { get; set; } = true;
    /// <summary>Basis-Spread in % (Bid-Ask-Spread, wird auf Entry/Exit addiert).</summary>
    public decimal SpreadPercent { get; set; } = 0.08m;
    /// <summary>Simulierte Latenz in Millisekunden.</summary>
    public int MaxLatencyMs { get; set; } = 300;
    /// <summary>Order-Rejection-Rate in % (Margin-Druck, Netzwerk-Fehler).</summary>
    public decimal OrderRejectionPercent { get; set; } = 2m;
    /// <summary>Minimaler Slippage-Multiplikator bei ruhigem Markt (auf ATR-Basis).</summary>
    public decimal MinSlippageAtrMultiplier { get; set; } = 0.1m;
    /// <summary>Maximaler Slippage-Multiplikator bei volatilem Markt (auf ATR-Basis).</summary>
    public decimal MaxSlippageAtrMultiplier { get; set; } = 0.5m;

    // === Partial Close ===
    /// <summary>Anteil der Position bei TP1 (161.8% Extension) geschlossen. SK: 50%.</summary>
    public decimal Tp1CloseRatio { get; set; } = 0.5m;
    /// <summary>Anteil der Position bei TP2 (200% + Buffer) geschlossen. SK: 50% Rest.</summary>
    public decimal Tp2CloseRatio { get; set; } = 0.5m;
    /// <summary>Max Stunden bevor Time-Exit greift (0 = unbegrenzt, Buch-konform).</summary>
    public int MaxHoldHoursInitial { get; set; } = 0;

    // === Minimum Risk-Reward-Ratio ===
    /// <summary>Minimales Risiko-Ertrags-Verhältnis. SK-Buch: min 1:1.</summary>
    public decimal MinRiskRewardRatio { get; set; } = 1.0m;

    // === Higher-Timeframe Confirmation ===
    /// <summary>Higher-Timeframe für Trend-Konfirmation im Backtest. null = keine HTF.</summary>
    public BingXBot.Core.Enums.TimeFrame? HtfTimeFrame { get; set; }

    /// <summary>Entry-Timeframe (M30 für SK-Buch).</summary>
    public BingXBot.Core.Enums.TimeFrame? EntryTimeFrame { get; set; }
}
