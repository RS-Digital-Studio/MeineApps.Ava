using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

/// <summary>
/// Zentrale Default-Werte für Trading-Modi.
/// Nach Buch-Refactoring (12.04.2026): nur noch SK-System als Strategie.
/// Die Modi bestimmen nur noch Scanner-Timeframe, Risiko und Recovery-SL-Multiplikator.
/// </summary>
public static class TradingModeDefaults
{
    /// <summary>Risiko-Parameter pro Modus. SK-Buch: 1% Risiko pro Trade.</summary>
    public static RiskPreset GetRiskPreset(TradingModePreset mode) => mode switch
    {
        // Alle Modi laufen SK-System → Buch-konformes 50/50 Partial Close (161.8%/200%)
        TradingModePreset.Scalping => new(
            MaxPositionSizePercent: 3m, MaxMarginPerTradePercent: 1m, MaxLeverage: 5m,
            CooldownHours: 0,
            MaxHoldHours: 8,
            Tp1CloseRatio: 0.5m, Tp2CloseRatio: 0.5m,
            MinRiskRewardRatio: 0m),

        TradingModePreset.DayTrading => new(
            MaxPositionSizePercent: 3m, MaxMarginPerTradePercent: 1m, MaxLeverage: 5m,
            CooldownHours: 0,
            MaxHoldHours: 24,
            Tp1CloseRatio: 0.5m, Tp2CloseRatio: 0.5m,
            MinRiskRewardRatio: 0m),

        // Swing (SK-System) — Buch: 50% bei TP1, 50% bei TP2, 1% Risiko, max 3 Trades
        _ => new(
            MaxPositionSizePercent: 3m, MaxMarginPerTradePercent: 1m, MaxLeverage: 10m,
            CooldownHours: 0,
            MaxHoldHours: 0,
            Tp1CloseRatio: 0.5m, Tp2CloseRatio: 0.5m,
            MinRiskRewardRatio: 0m),
    };

    /// <summary>Scanner-Parameter pro Modus. SK-System nutzt Swing-Preset (H4 Scanner, M30 Entry).</summary>
    public static ScannerPreset GetScannerPreset(TradingModePreset mode) => mode switch
    {
        TradingModePreset.Scalping => new(
            ScanTimeFrame: TimeFrame.M15,
            MinVolume24h: 30_000_000m,
            MinPriceChange: 0.2m,
            MaxResults: 30),

        TradingModePreset.DayTrading => new(
            ScanTimeFrame: TimeFrame.H1,
            MinVolume24h: 20_000_000m,
            MinPriceChange: 0.3m,
            MaxResults: 40),

        // Swing (SK-System) — Buch-konform: H4 Navigator, Reversal, breites Screening
        _ => new(
            ScanTimeFrame: TimeFrame.H4,
            MinVolume24h: 10_000_000m,
            MinPriceChange: 0.1m,
            MaxResults: 50,
            Mode: ScanMode.Reversal),
    };

    /// <summary>
    /// ATR-basierter SL-Multiplikator für Position-Recovery nach App-Neustart (Fallback wenn kein Signal bekannt).
    /// Einheitlich für SK-System: 2× ATR ist ein pragmatischer Notfall-SL.
    /// </summary>
    public static decimal GetRecoverySlMultiplier(int atrPercentile) => atrPercentile switch
    {
        < 20 => 1.5m,
        < 50 => 1.8m,
        < 75 => 2.0m,
        < 90 => 2.5m,
        _    => 2.0m,
    };
}

/// <summary>Risiko-Parameter-Preset (SK-Buch-konform).</summary>
public record RiskPreset(
    decimal MaxPositionSizePercent, decimal MaxMarginPerTradePercent, decimal MaxLeverage,
    int CooldownHours,
    int MaxHoldHours,
    decimal Tp1CloseRatio, decimal Tp2CloseRatio,
    decimal MinRiskRewardRatio);

/// <summary>Scanner-Parameter-Preset.</summary>
public record ScannerPreset(
    TimeFrame ScanTimeFrame,
    decimal MinVolume24h, decimal MinPriceChange,
    int MaxResults,
    bool OnlyTopByVolume = true, int TopCoinsCount = 100,
    ScanMode? Mode = null);
