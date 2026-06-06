namespace BingXBot.Core.Configuration;

/// <summary>
/// Parameter der Cross-Sectional-Momentum-Strategie (market-neutral: long staerkste / short schwaechste
/// Symbole, periodischer Rebalance). Backtest-validiert (06.06.2026): in ALLEN 4 Marktphasen positiv bei
/// <c>L120/R~monatlich/radj/1x</c>; Slot-Anzahl skaliert mit Universums-Breite (breit → 5L-5S). Persistierbar
/// analog <see cref="RiskSettings"/>/<see cref="ScannerSettings"/>.
/// </summary>
public sealed class CrossSectionalSettings
{
    /// <summary>Momentum-Lookback in Nav-Kerzen (H4). 120 ≈ 20 Tage.</summary>
    public int LookbackCandles { get; set; } = 120;

    /// <summary>Rebalance-Intervall in TAGEN (Wall-Clock, robust gegen Pi-Downtime). 21 ≈ monatlich.</summary>
    public int RebalanceDays { get; set; } = 21;

    /// <summary>Anzahl Long-Slots (staerkste Momentum-Symbole). Breites Universum (Top-100+) → 5.</summary>
    public int LongK { get; set; } = 5;

    /// <summary>Anzahl Short-Slots (schwaechste). Breites Universum → 5.</summary>
    public int ShortK { get; set; } = 5;

    /// <summary>Momentum vol-bereinigen (ROC / ATR%) — macht unterschiedlich volatile Symbole vergleichbar.</summary>
    public bool RiskAdjusted { get; set; } = true;

    /// <summary>
    /// Leverage-Obergrenze pro Position. KRITISCH = 1: der Backtest zeigt lev1 Bear +10%, lev5 Bear −81%
    /// (5×-Default macht die Strategie auf dem Crypto-Korb zur Lotterie). Default 1.
    /// </summary>
    public int LeverageCap { get; set; } = 1;

    /// <summary>Equity-Auslastung pro Rebalance (≤ MaxTotalMargin/100, laesst Puffer fuer Fees/Slippage).</summary>
    public decimal MarginUtilization { get; set; } = 0.75m;

    /// <summary>Per-Position-ATR-Stop zwischen Rebalances (0 = kein Stop; die validierte v1-Config ist ohne robust).</summary>
    public decimal AtrStopMultiplier { get; set; } = 0m;

    /// <summary>Universums-Groesse (Top-N nach 24h-Volumen). Breite ist noetig fuer den Dispersions-Edge.</summary>
    public int UniverseTopN { get; set; } = 100;

    /// <summary>TradFi-Perps (NC-Prefix) ins Universum aufnehmen (zusaetzliche Diversifikation).</summary>
    public bool IncludeTradFi { get; set; } = true;

    /// <summary>Nav-Timeframe fuer Momentum/Kerzen. Default H4 (wie Backtest).</summary>
    public string NavTimeframe { get; set; } = "H4";

    /// <summary>Tick-Intervall der Pruefung „ist Rebalance faellig?" in Minuten (kein Trade dazwischen).</summary>
    public int CheckIntervalMinutes { get; set; } = 30;
}
