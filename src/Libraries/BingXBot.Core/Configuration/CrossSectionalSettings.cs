namespace BingXBot.Core.Configuration;

/// <summary>
/// Parameter der Cross-Sectional-Momentum-Strategie (market-neutral: long staerkste / short schwaechste
/// Symbole, periodischer Rebalance). Backtest-validiert: in ALLEN 4 Marktphasen positiv bei
/// <c>L120/R~monatlich/radj/1x</c> auf dem Top-50-Universum INKL. TradFi mit 3L-3S (min +14,2 %,
/// Σ +202 % ueber 4 Phasen bei 154 USDT). Wichtig: Auf Top-100 ist KEINE Config phasen-robust
/// (5L-5S dort min −17,2 %) und OHNE TradFi kippt auch Top-50 — die Cross-Asset-Dispersion
/// (Gold/Indizes/Forex) traegt den Edge. Persistierbar analog <see cref="RiskSettings"/>.
/// </summary>
public sealed class CrossSectionalSettings
{
    /// <summary>Momentum-Lookback in Nav-Kerzen (H4). 120 ≈ 20 Tage.</summary>
    public int LookbackCandles { get; set; } = 120;

    /// <summary>Rebalance-Intervall in TAGEN (Wall-Clock, robust gegen Pi-Downtime). 21 ≈ monatlich.</summary>
    public int RebalanceDays { get; set; } = 21;

    /// <summary>Anzahl Long-Slots (staerkste Momentum-Symbole). Top-50-Universum → 3 (phasen-robustestes Profil).</summary>
    public int LongK { get; set; } = 3;

    /// <summary>Anzahl Short-Slots (schwaechste). Top-50-Universum → 3.</summary>
    public int ShortK { get; set; } = 3;

    /// <summary>Momentum vol-bereinigen (ROC / ATR%) — macht unterschiedlich volatile Symbole vergleichbar.</summary>
    public bool RiskAdjusted { get; set; } = true;

    /// <summary>
    /// Leverage-Obergrenze pro Position. Der Hebel ist ein reiner Multiplikator des Profil-Ergebnisses:
    /// Auf dem Live-Profil (Top-50+TradFi, 3L-3S) ist 2x in allen 4 Phasen positiv (Σ +548 % vs.
    /// +250 % bei 1x ueber 4 Jahre) — ab 3x kippt die Recovery-Phase (−12 %), 5x ist Lotterie (−36 %).
    /// Default 2 (bewusste User-Entscheidung 10.06.2026); 1 = konservative Basis.
    /// </summary>
    public int LeverageCap { get; set; } = 2;

    /// <summary>Equity-Auslastung pro Rebalance (≤ MaxTotalMargin/100, laesst Puffer fuer Fees/Slippage).</summary>
    public decimal MarginUtilization { get; set; } = 0.75m;

    /// <summary>Per-Position-ATR-Stop zwischen Rebalances (0 = kein Stop; die validierte v1-Config ist ohne robust).</summary>
    public decimal AtrStopMultiplier { get; set; } = 0m;

    /// <summary>
    /// Universums-Groesse (Top-N nach 24h-Volumen). Top-50 ist das validierte Profil — Top-100
    /// verwaessert das Ranking (kein phasen-robustes K auf Top-100 im 4-Phasen-Screen).
    /// </summary>
    public int UniverseTopN { get; set; } = 50;

    /// <summary>
    /// TradFi-Perps (NC-Prefix) ins Universum aufnehmen. PFLICHT fuer den Edge: ohne TradFi ist
    /// auch Top-50 in keiner Config phasen-robust (Crypto allein liefert zu wenig Dispersion).
    /// </summary>
    public bool IncludeTradFi { get; set; } = true;

    /// <summary>Nav-Timeframe fuer Momentum/Kerzen. Default H4 (wie Backtest).</summary>
    public string NavTimeframe { get; set; } = "H4";

    /// <summary>Tick-Intervall der Pruefung „ist Rebalance faellig?" in Minuten (kein Trade dazwischen).</summary>
    public int CheckIntervalMinutes { get; set; } = 30;
}
