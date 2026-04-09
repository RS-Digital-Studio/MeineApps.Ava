using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

/// <summary>
/// Zentrale Default-Werte für alle 3 Trading-Modi.
/// Basierend auf Krypto-Futures-Recherche (2024-2026).
/// </summary>
public static class TradingModeDefaults
{
    /// <summary>Strategie-Parameter pro Modus.</summary>
    public static StrategyPreset GetStrategyPreset(TradingModePreset mode) => mode switch
    {
        // Scalping: Optimiert für M15 Krypto-Futures (Whipsaw-Reduktion, höhere Signalqualität)
        // Supertrend 10/2.5 statt 7/2.0: ~40% weniger Whipsaws, 2.5h Signallänge statt 1.75h
        // MinScore 6: Höhere Confluence (M15 hat mehr Noise als H4, braucht strengeren Filter)
        // ADX 20: Echte Trends, nicht Drift (ADX 15 war fast immer erfüllt)
        // Volume 1.5x: Filtert schwach-volumige M15-Candles mit hohem Spread
        // RSI asymmetrisch: Krypto-Uptrends halten RSI bei 55-75, symmetrisch 35-65 verpasst das
        TradingModePreset.Scalping => new(
            SupertrendPeriod: 10, SupertrendMultiplier: 2.5m,
            EmaFast: 9, EmaSlow: 21, EmaTrendFilter: 50,
            AdxPeriod: 10, MinAdx: 18m,   // 18: Kompromiss zwischen 15 (zu locker) und 20 (zu streng auf M15)
            RsiPeriod: 7, RsiLongMin: 40m, RsiLongMax: 75m, RsiShortMin: 25m, RsiShortMax: 60m,
            VolumePeriod: 14, VolumeMultiplier: 1.3m,  // 1.3: Kompromiss (1.5 filtert zu viel auf M15)
            AtrPeriod: 7, AtrPercentileLookback: 50,
            MinScore: 5,  // 5: Zusammen mit strengerem Supertrend/ADX/Volume reicht das
            HtfConfirmationTimeframe: TimeFrame.H1),

        // DayTrading: H1-optimiert, ausgewogene Signalqualität
        // MinScore 6: Gleich wie Scalping/Swing (war 5, zu viele schwache Signale)
        // Volume 1.3x: Stärker als vorher, H1-Volumen ist stabiler als M15
        // RSI asymmetrisch: Krypto-Uptrends halten RSI bei 50-75 auf H1
        TradingModePreset.DayTrading => new(
            SupertrendPeriod: 10, SupertrendMultiplier: 3.0m,
            EmaFast: 9, EmaSlow: 21, EmaTrendFilter: 50,
            AdxPeriod: 14, MinAdx: 20m,
            RsiPeriod: 14, RsiLongMin: 40m, RsiLongMax: 75m, RsiShortMin: 25m, RsiShortMax: 60m,
            VolumePeriod: 20, VolumeMultiplier: 1.2m,
            AtrPeriod: 10, AtrPercentileLookback: 75,
            MinScore: 5,
            HtfConfirmationTimeframe: TimeFrame.H4),

        // Swing: Konservativ, breitere Perioden, höhere Qualitäts-Schwellen
        _ => new(
            SupertrendPeriod: 10, SupertrendMultiplier: 3.0m,
            EmaFast: 12, EmaSlow: 26, EmaTrendFilter: 50,
            AdxPeriod: 14, MinAdx: 20m,  // 20: Standard-Schwelle für Trend-Erkennung
            RsiPeriod: 14, RsiLongMin: 42m, RsiLongMax: 78m, RsiShortMin: 22m, RsiShortMax: 58m,
            VolumePeriod: 20, VolumeMultiplier: 1.2m,  // 1.2x: Echte Volume-Bestätigung
            AtrPeriod: 14, AtrPercentileLookback: 100,
            MinScore: 6,
            HtfConfirmationTimeframe: TimeFrame.D1),
    };

    /// <summary>Risiko-Parameter pro Modus.</summary>
    public static RiskPreset GetRiskPreset(TradingModePreset mode) => mode switch
    {
        // Scalping: 100% bei TP1 schließen — kein Trailing
        // Multi-Stage Exit REDUZIERT Gewinne bei Scalping (Trailing-Rest landet am BE)
        // Besser: Voller Gewinn bei TP1 sofort mitnehmen
        TradingModePreset.Scalping => new(
            MaxPositionSizePercent: 10m, MaxMarginPerTradePercent: 1m, MaxLeverage: 3m,
            CooldownHours: 0, MaxCooldownHours: 0,
            MaxHoldHours: 8, MaxHoldHoursAfterTp1: 0,
            Tp1CloseRatio: 1.0m, Tp2CloseRatio: 0m,       // 100% bei TP1 → voller Gewinn
            SmartBreakevenAtrMultiplier: 0m,
            MinRiskRewardRatio: 0m),

        // DayTrading: 100% bei TP1 — gleiche Logik wie Scalping
        TradingModePreset.DayTrading => new(
            MaxPositionSizePercent: 15m, MaxMarginPerTradePercent: 1.5m, MaxLeverage: 3m,
            CooldownHours: 0, MaxCooldownHours: 0,
            MaxHoldHours: 24, MaxHoldHoursAfterTp1: 0,
            Tp1CloseRatio: 1.0m, Tp2CloseRatio: 0m,       // 100% bei TP1
            SmartBreakevenAtrMultiplier: 0m,
            MinRiskRewardRatio: 0m),

        // Swing: 70% bei TP1, 30% Trailing — Swing-Trends können weiterlaufen
        _ => new(
            MaxPositionSizePercent: 20m, MaxMarginPerTradePercent: 2m, MaxLeverage: 3m,
            CooldownHours: 0, MaxCooldownHours: 0,
            MaxHoldHours: 48, MaxHoldHoursAfterTp1: 96,
            Tp1CloseRatio: 0.7m, Tp2CloseRatio: 0.2m,     // 70/20/10
            SmartBreakevenAtrMultiplier: 0.3m,
            MinRiskRewardRatio: 0m),
    };

    /// <summary>Scanner-Parameter pro Modus.</summary>
    public static ScannerPreset GetScannerPreset(TradingModePreset mode) => mode switch
    {
        TradingModePreset.Scalping => new(
            ScanTimeFrame: TimeFrame.M15,
            MinVolume24h: 30_000_000m,   // 30M statt 100M: Mehr Coins, Top-100 Filter schützt
            MinPriceChange: 0.2m,         // 0.2% statt 0.3%: Mehr Kandidaten
            MaxResults: 30,               // 30 statt 10: Deutlich mehr Symbole pro Scan
            UseM15EntryTiming: false),

        TradingModePreset.DayTrading => new(
            ScanTimeFrame: TimeFrame.H1,
            MinVolume24h: 20_000_000m,   // 20M statt 50M
            MinPriceChange: 0.3m,         // 0.3% statt 0.5%
            MaxResults: 40,               // 40 statt 15
            UseM15EntryTiming: true),

        _ => new( // Swing
            ScanTimeFrame: TimeFrame.H4,
            MinVolume24h: 10_000_000m,   // 10M statt 20M
            MinPriceChange: 0.3m,         // 0.3% statt 0.5%
            MaxResults: 50,               // 50 statt 20
            UseM15EntryTiming: true),
    };

    /// <summary>Vol-adaptive SL/TP-Multiplikatoren pro Modus + ATR-Perzentil.</summary>
    public static (decimal slMult, decimal tp1Mult, decimal tp2Mult, decimal trailMult)
        GetVolAdaptiveMultipliers(TradingModePreset mode, int atrPercentile)
    {
        return mode switch
        {
            // Scalping: Enges SL (1.0-1.5x ATR), TP weiter (2.5-3.5x ATR) → bessere RRR
            // TrailMult 1.2-1.5: Enges Trailing nach TP1, sichert Gewinne schnell
            TradingModePreset.Scalping => atrPercentile switch
            {                // SL     TP1    TP2    Trail
                < 20  => (1.0m, 2.5m, 4.0m, 1.2m),  // Ruhig: Enges SL OK
                < 50  => (1.2m, 2.8m, 4.5m, 1.3m),  // Normal
                < 75  => (1.3m, 3.0m, 5.0m, 1.5m),  // Volatil: Etwas mehr Platz
                < 90  => (1.5m, 3.5m, 5.5m, 1.8m),  // Sehr volatil
                _     => (1.2m, 2.5m, 4.0m, 1.3m),  // Extrem: Konservativ
            },
            // DayTrading: Moderate Werte, gute RRR
            TradingModePreset.DayTrading => atrPercentile switch
            {
                < 20  => (1.2m, 2.5m, 4.0m, 1.5m),
                < 50  => (1.5m, 3.0m, 5.0m, 1.8m),
                < 75  => (1.8m, 3.5m, 5.5m, 2.0m),
                < 90  => (2.0m, 4.0m, 6.0m, 2.2m),
                _     => (1.5m, 2.5m, 4.0m, 1.5m),
            },
            // Swing: Breitere Werte, Trends laufen lassen
            _ => atrPercentile switch
            {
                < 20  => (1.5m, 3.0m, 5.0m, 2.0m),
                < 50  => (1.8m, 3.5m, 6.0m, 2.2m),
                < 75  => (2.0m, 4.0m, 7.0m, 2.5m),
                < 90  => (2.5m, 5.0m, 8.0m, 3.0m),
                _     => (2.0m, 3.5m, 5.5m, 2.0m),
            }
        };
    }
}

/// <summary>Strategie-Parameter-Preset (alle Felder für CryptoTrendPro).</summary>
public record StrategyPreset(
    int SupertrendPeriod, decimal SupertrendMultiplier,
    int EmaFast, int EmaSlow, int EmaTrendFilter,
    int AdxPeriod, decimal MinAdx,
    int RsiPeriod, decimal RsiLongMin, decimal RsiLongMax, decimal RsiShortMin, decimal RsiShortMax,
    int VolumePeriod, decimal VolumeMultiplier,
    int AtrPeriod, int AtrPercentileLookback,
    int MinScore,
    TimeFrame HtfConfirmationTimeframe);

/// <summary>Risiko-Parameter-Preset (Teilmenge von RiskSettings die sich pro Modus ändern).</summary>
public record RiskPreset(
    decimal MaxPositionSizePercent, decimal MaxMarginPerTradePercent, decimal MaxLeverage,
    int CooldownHours, int MaxCooldownHours,
    int MaxHoldHours, int MaxHoldHoursAfterTp1,
    decimal Tp1CloseRatio, decimal Tp2CloseRatio,
    decimal SmartBreakevenAtrMultiplier,
    decimal MinRiskRewardRatio);

/// <summary>Scanner-Parameter-Preset.</summary>
public record ScannerPreset(
    TimeFrame ScanTimeFrame,
    decimal MinVolume24h, decimal MinPriceChange,
    int MaxResults, bool UseM15EntryTiming,
    bool OnlyTopByVolume = true, int TopCoinsCount = 100);
