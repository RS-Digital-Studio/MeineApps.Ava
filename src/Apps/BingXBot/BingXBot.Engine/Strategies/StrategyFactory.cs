using BingXBot.Core.Interfaces;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Factory für Strategie-Instanzen. Trend-Following-Familie (Donchian-Breakout mit dem Markt,
/// Market-Entry, ATR-SL, RRR-TP). Varianten unterscheiden sich nur in den Parametern.
/// Live-Default: TrendFollow-Fast (Donchian 10 / EMA 34 / ADX 18 / ATR-SL 2.75).
/// </summary>
public static class StrategyFactory
{
    /// <summary>Verfügbare Strategien für die UI/Produktion. Live-Default: TrendFollow-Fast.</summary>
    public static readonly string[] AvailableStrategies = ["TrendFollow-Fast"];

    /// <summary>Erstellt eine neue IStrategy-Instanz basierend auf dem Namen.</summary>
    public static IStrategy Create(string name) => name switch
    {
        // Trend-Following-Familie — Donchian-Breakout in Trend-Richtung, Market-Entry, ATR-SL, RRR-TP.
        "TrendFollow" => new TrendFollowStrategy(),
        // Schneller: kuerzere Donchian/EMA, mehr Signale auf niedrigeren TFs.
        // atrSlMultiplier 2.75 (statt Default 2.5): Backtest-Lab-Sweep (--full, 21 may-live-Symbole,
        // durchgehend 2024-06..2026-05, alle Marktphasen) → 2.75 ist das robuste SL-Optimum: PF 3.75 vs 2.53,
        // WinRate 70.7 % vs 65.7 %, Shorts 76 % vs 65 % WR. Glatter Peak (2.5↗2.75↘3.0), >3.0 bricht ein.
        "TrendFollow-Fast" => new TrendFollowStrategy(donchianPeriod: 10, emaPeriod: 34, adxMin: 18m, atrSlMultiplier: 2.75m),
        // Fast + Chop-Filter (ADX steigend) — gegen Seitwaerts-Whipsaw.
        "TrendFollow-Fast-Chop" => new TrendFollowStrategy(donchianPeriod: 10, emaPeriod: 34, adxMin: 18m, requireRisingAdx: true),
        // Fast + Mindest-Ausbruchsdistanz 0.5xATR — gegen Knapp-Fakeouts.
        "TrendFollow-Fast-BO" => new TrendFollowStrategy(donchianPeriod: 10, emaPeriod: 34, adxMin: 18m, minBreakoutAtr: 0.5m),
        // Fast + beide Filter.
        "TrendFollow-Fast-ChopBO" => new TrendFollowStrategy(donchianPeriod: 10, emaPeriod: 34, adxMin: 18m, requireRisingAdx: true, minBreakoutAtr: 0.5m),
        // Defensiver: weiterer SL, hoeheres RRR — weniger SL-Hits durch Rauschen, groessere Gewinner.
        "TrendFollow-Wide" => new TrendFollowStrategy(donchianPeriod: 20, atrSlMultiplier: 3.5m, tp1Rrr: 2.0m, tp2Rrr: 4.0m),
        // Strenger Trendfilter: nur sehr starke Trends (ADX >= 25), langsame EMA.
        "TrendFollow-Strong" => new TrendFollowStrategy(donchianPeriod: 30, emaPeriod: 100, adxMin: 25m, atrSlMultiplier: 3.0m, tp1Rrr: 1.5m, tp2Rrr: 3.5m),

        // Mean-Reversion-Falsifikationsexperiment (NICHT in AvailableStrategies — nur Lab-Test, noch nicht
        // produktiv). Bollinger-Band-Fade im Range-Regime (ADX<20) gegen den nicht-organischen Flow
        // zwangsliquidierter Retail-Trader. Standard-Params festgenagelt (Overfitting-Schutz).
        "MeanReversion" => new MeanReversionStrategy(),

        _ => throw new ArgumentException($"Unbekannte Strategie: {name}")
    };
}
