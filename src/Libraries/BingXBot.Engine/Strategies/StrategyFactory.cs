using BingXBot.Core.Interfaces;

namespace BingXBot.Engine.Strategies;

/// <summary>
/// Factory für Strategie-Instanzen. Neben dem SK-System (Reversal nach Stefan Kassing)
/// gibt es die Trend-Following-Familie (Donchian-Breakout mit dem Markt) als robusten,
/// backtest-treuen Gegenentwurf. Varianten unterscheiden sich nur in den Parametern.
/// </summary>
public static class StrategyFactory
{
    /// <summary>Verfügbare Strategien für die UI/Produktion.</summary>
    public static readonly string[] AvailableStrategies = ["SK-System", "TrendFollow"];

    /// <summary>Erstellt eine neue IStrategy-Instanz basierend auf dem Namen.</summary>
    public static IStrategy Create(string name) => name switch
    {
        "SK-System" => new SequenzKonzeptStrategy(),

        // Trend-Following-Familie — Donchian-Breakout in Trend-Richtung, Market-Entry, ATR-SL, RRR-TP.
        "TrendFollow" => new TrendFollowStrategy(),
        // Schneller: kuerzere Donchian/EMA, mehr Signale auf niedrigeren TFs.
        "TrendFollow-Fast" => new TrendFollowStrategy(donchianPeriod: 10, emaPeriod: 34, adxMin: 18m),
        // Defensiver: weiterer SL, hoeheres RRR — weniger SL-Hits durch Rauschen, groessere Gewinner.
        "TrendFollow-Wide" => new TrendFollowStrategy(donchianPeriod: 20, atrSlMultiplier: 3.5m, tp1Rrr: 2.0m, tp2Rrr: 4.0m),
        // Strenger Trendfilter: nur sehr starke Trends (ADX >= 25), langsame EMA.
        "TrendFollow-Strong" => new TrendFollowStrategy(donchianPeriod: 30, emaPeriod: 100, adxMin: 25m, atrSlMultiplier: 3.0m, tp1Rrr: 1.5m, tp2Rrr: 3.5m),

        // Reparierte SK: SK-Sequenz-Trigger + Trend-Filter + Market-Entry + ATR-SL/RRR.
        "SkTrend" => new SkTrendStrategy(),
        "SkTrend-Wide" => new SkTrendStrategy(atrSlMultiplier: 3.0m, tp1Rrr: 2.0m, tp2Rrr: 4.0m),

        _ => throw new ArgumentException($"Unbekannte Strategie: {name}")
    };
}
