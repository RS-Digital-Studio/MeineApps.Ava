namespace BingXBot.Core.Configuration;

public class BacktestSettings
{
    public decimal InitialBalance { get; set; } = 1000m;
    public decimal MakerFee { get; set; } = 0.0002m;
    public decimal TakerFee { get; set; } = 0.0005m;
    public decimal SlippagePercent { get; set; } = 0.05m;
    public bool SimulateFundingRate { get; set; } = true;
    /// <summary>Simulierte Funding-Rate pro 8h-Intervall in % (z.B. 0.01 = 0.01%). Nur aktiv wenn SimulateFundingRate=true.</summary>
    public decimal SimulatedFundingRatePercent { get; set; } = 0.01m;

    // === Realistisches Markt-Modell ===
    /// <summary>Dynamische Slippage basierend auf ATR/Volumen statt fixem Prozentsatz.</summary>
    public bool UseDynamicSlippage { get; set; } = true;
    /// <summary>Basis-Spread in % (Bid-Ask-Spread, wird auf Entry/Exit addiert). Realistisch: 0.05-0.15% für Majors.</summary>
    public decimal SpreadPercent { get; set; } = 0.08m;
    /// <summary>Simulierte Latenz in Millisekunden (Random 0 bis MaxLatencyMs). Beeinflusst Fill-Preis bei schnellen Moves.</summary>
    public int MaxLatencyMs { get; set; } = 300;
    /// <summary>Order-Rejection-Rate in % (Margin-Druck, Netzwerk-Fehler). 0 = keine Rejections.</summary>
    public decimal OrderRejectionPercent { get; set; } = 2m;
    /// <summary>Minimaler Slippage-Multiplikator bei ruhigem Markt (auf ATR-Basis).</summary>
    public decimal MinSlippageAtrMultiplier { get; set; } = 0.1m;
    /// <summary>Maximaler Slippage-Multiplikator bei volatilem Markt (auf ATR-Basis).</summary>
    public decimal MaxSlippageAtrMultiplier { get; set; } = 0.5m;
    /// <summary>ATR-Multiplikator für Smart Breakeven nach TP1 (SL = Entry + X*ATR statt Entry exakt). 0 = klassisches BE.</summary>
    public decimal SmartBreakevenAtrMultiplier { get; set; } = 0.5m;

    // === Multi-Stage Exit ===
    /// <summary>Multi-Stage Exit im Backtest simulieren (TP1 Partial, BE-Move, Trailing, Time-Exit).</summary>
    public bool SimulateMultiStageExit { get; set; } = true;
    /// <summary>Anteil der Position, der bei TP1 geschlossen wird (0.3 = 30%). Pyramid: TP1=30%, TP2=30%, Trailing=40%.</summary>
    public decimal Tp1CloseRatio { get; set; } = 0.3m;
    /// <summary>Anteil der Position, der bei TP2 geschlossen wird (0.3 = 30%). Rest bleibt für Trailing.</summary>
    public decimal Tp2CloseRatio { get; set; } = 0.3m;
    /// <summary>Chandelier-Trailing ATR-Multiplikator nach TP1.</summary>
    public decimal TrailingAtrMultiplier { get; set; } = 2.5m;
    /// <summary>Max Stunden ohne TP1 bevor Time-Exit greift.</summary>
    public int MaxHoldHoursInitial { get; set; } = 48;
    /// <summary>Max Stunden nach TP1 bevor Rest geschlossen wird.</summary>
    public int MaxHoldHoursAfterTp1 { get; set; } = 96;

    // === Minimum Risk-Reward-Ratio ===
    /// <summary>Minimales Risiko-Ertrags-Verhältnis (TP-Distanz / SL-Distanz). Trades unter diesem Wert werden abgelehnt.</summary>
    public decimal MinRiskRewardRatio { get; set; } = 1.5m;

    // === Higher-Timeframe Confirmation ===
    /// <summary>
    /// Higher-Timeframe für Trend-Konfirmation im Backtest. null = keine HTF-Confirmation.
    /// Wird aus dem Trading-Modus-Preset automatisch gesetzt (M15→H1, H1→H4, H4→D1).
    /// </summary>
    public BingXBot.Core.Enums.TimeFrame? HtfTimeFrame { get; set; }

    /// <summary>Entry-Timeframe für SK-System 3. Ebene (M5 für M15, M15 für H1). null = automatisch bestimmen.</summary>
    public BingXBot.Core.Enums.TimeFrame? EntryTimeFrame { get; set; }
}
