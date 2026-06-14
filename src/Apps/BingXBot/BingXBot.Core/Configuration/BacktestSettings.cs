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

    // === Minimum Risk-Reward-Ratio ===
    /// <summary>Minimales Risiko-Ertrags-Verhältnis. SK-Buch: min 1:1.</summary>
    public decimal MinRiskRewardRatio { get; set; } = 1.0m;

    // === Higher-Timeframe Confirmation ===
    /// <summary>Higher-Timeframe für Trend-Konfirmation im Backtest. null = keine HTF.</summary>
    public BingXBot.Core.Enums.TimeFrame? HtfTimeFrame { get; set; }

    /// <summary>Entry-Timeframe (M30 für SK-Buch).</summary>
    public BingXBot.Core.Enums.TimeFrame? EntryTimeFrame { get; set; }

    // === Live-Spiegel-Vorfilter (nur Portfolio-Backtest) ===
    // Schaltet die Live-Scanner-/BTC-Health-Pfade im PortfolioBacktestEngine zu, damit der Backtest
    // EXAKT wie der Live-Bot filtert + sized. Default = false (Backward-Compat: bestehende Laeufe
    // bleiben bit-identisch). Im --portfolio-Lab-Modus standardmaessig AN ("alles wie in live").

    /// <summary>
    /// GAP 11: Live-Scanner-Vorfilter (MinVolume24h + MinPriceChange pro Nav-TF, kategorie-spezifisch
    /// Crypto/TradFi wie <c>ScanHelper.FilterCandidatesForTimeframe</c>) + TradFi-Marktstunden
    /// (<c>TradingHoursFilter.IsMarketOpen</c>) + Crypto-Session-Bitmask
    /// (<c>TradingHoursFilter.IsSessionAllowed</c>). Symbol/Zeitschritt, der den Filter nicht passiert,
    /// erzeugt keinen Entry-Versuch. Nur im <c>PortfolioBacktestEngine</c> wirksam.
    /// </summary>
    public bool EnableScannerPrefilter { get; set; }

    /// <summary>
    /// GAP 4: BTC-Health-Positionsskalierung (<c>MarketFilter.CalculateBtcHealth</c> pro Zeitschritt aus
    /// inkrementellen BTC-D1/H4-Slices). Bei Crypto: harter Block wenn <c>AllowLong/AllowShort</c>=false,
    /// sonst Multiplikation der Positionsgroesse mit <c>PositionScale</c> (0.65..1.0). Die SK-Score-Skalierung
    /// (ConfluenceScore ≥10→1.25 / ≥5→1.0 / sonst→0.75) ist an dieses Flag gekoppelt (live-treues Sizing).
    /// Nur im <c>PortfolioBacktestEngine</c> wirksam.
    /// </summary>
    public bool EnableBtcHealthScale { get; set; }
}
