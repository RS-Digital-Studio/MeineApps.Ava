namespace BingXBot.Core.Configuration;

/// <summary>
/// Parameter der Cross-Sectional-Momentum-Strategie (market-neutral: long staerkste / short schwaechste
/// Symbole, periodischer Rebalance). Backtest-validiert: in ALLEN 4 Marktphasen positiv bei
/// <c>L60/R9d/radj/2x</c> auf dem Top-50-Universum INKL. TradFi mit 3L-3S (min +28,3 %,
/// Σ +297 % ueber 4 Phasen bei 154 USDT). Das fruehere L120/R21-Profil ist auf demselben Universum
/// nur 2/4 Phasen positiv (min −50,6 %); das kuerzere 10-Tage-Lookback / ~woechentliche Rebalance
/// deckt sich mit der Momentum-Literatur. Vol-Targeting verschlechtert das Top-50-Profil (vt30 senkt
/// die schwaechste Phase auf +7,8 %) — daher NICHT als Feld aufgenommen. Wichtig: Auf Top-100 ist
/// KEINE Config phasen-robust und OHNE TradFi kippt auch Top-50 — die Cross-Asset-Dispersion
/// (Gold/Indizes/Forex) traegt den Edge. Persistierbar analog <see cref="RiskSettings"/>.
/// </summary>
public sealed class CrossSectionalSettings
{
    /// <summary>Momentum-Lookback in Nav-Kerzen (H4). 60 ≈ 10 Tage (validiertes Optimum; L120 nur 2/4 Phasen).</summary>
    public int LookbackCandles { get; set; } = 60;

    /// <summary>Rebalance-Intervall in TAGEN (Wall-Clock, robust gegen Pi-Downtime). 9 ≈ woechentlich (validiert).</summary>
    public int RebalanceDays { get; set; } = 9;

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

    // Gate fuer atomares Clone/CopyFrom: Der REST-PUT (/settings/xsec) mutiert diese Singleton-
    // Instanz, waehrend der Xsec-Tick-Loop sie ueber viele Awaits hinweg liest. Ohne Atomaritaet
    // koennte ein Tick einen Korb mit altem LongK bilden, aber das Sizing mit neuem LongK+ShortK
    // rechnen (torn read → inkonsistente Slot-Dimensionierung mit Echtgeld). Der Tick liest daher
    // EINEN konsistenten Snapshot (Clone) zu Operations-Beginn; der PUT schreibt atomar (CopyFrom).
    private readonly object _gate = new();

    /// <summary>Atomarer, konsistenter Snapshot fuer einen Tick-Durchlauf (kein torn read mit PUT).</summary>
    public CrossSectionalSettings Clone()
    {
        lock (_gate)
        {
            return new CrossSectionalSettings
            {
                LookbackCandles = LookbackCandles,
                RebalanceDays = RebalanceDays,
                LongK = LongK,
                ShortK = ShortK,
                RiskAdjusted = RiskAdjusted,
                LeverageCap = LeverageCap,
                MarginUtilization = MarginUtilization,
                AtrStopMultiplier = AtrStopMultiplier,
                UniverseTopN = UniverseTopN,
                IncludeTradFi = IncludeTradFi,
                NavTimeframe = NavTimeframe,
                CheckIntervalMinutes = CheckIntervalMinutes,
            };
        }
    }

    /// <summary>Atomares In-Place-Update (Referenz-Identitaet bleibt — DI-Singleton, vom Manager gehalten).</summary>
    public void CopyFrom(CrossSectionalSettings src)
    {
        lock (_gate)
        {
            LookbackCandles = src.LookbackCandles;
            RebalanceDays = src.RebalanceDays;
            LongK = src.LongK;
            ShortK = src.ShortK;
            RiskAdjusted = src.RiskAdjusted;
            LeverageCap = src.LeverageCap;
            MarginUtilization = src.MarginUtilization;
            AtrStopMultiplier = src.AtrStopMultiplier;
            UniverseTopN = src.UniverseTopN;
            IncludeTradFi = src.IncludeTradFi;
            NavTimeframe = src.NavTimeframe;
            CheckIntervalMinutes = src.CheckIntervalMinutes;
        }
    }
}
