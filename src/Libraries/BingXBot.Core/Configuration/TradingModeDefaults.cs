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
        TradingModePreset.Scalping => new(
            SupertrendPeriod: 7, SupertrendMultiplier: 2.0m,
            EmaFast: 9, EmaSlow: 21, EmaTrendFilter: 50,
            AdxPeriod: 10, MinAdx: 15m,  // 15 statt 20: Mehr Signale, auch in ruhigeren Märkten
            RsiPeriod: 7, RsiLongMin: 35m, RsiLongMax: 65m, RsiShortMin: 35m, RsiShortMax: 65m,
            VolumePeriod: 14, VolumeMultiplier: 1.0m,  // 1.0x: Volume-Filter lockerer
            AtrPeriod: 7, AtrPercentileLookback: 50,
            MinScore: 5,  // 5 statt 7: Mehr Trades, auch bei BTC/ETH
            HtfConfirmationTimeframe: TimeFrame.H1),

        TradingModePreset.DayTrading => new(
            SupertrendPeriod: 10, SupertrendMultiplier: 2.5m,
            EmaFast: 9, EmaSlow: 21, EmaTrendFilter: 50,
            AdxPeriod: 14, MinAdx: 15m,
            RsiPeriod: 14, RsiLongMin: 40m, RsiLongMax: 70m, RsiShortMin: 30m, RsiShortMax: 60m,
            VolumePeriod: 20, VolumeMultiplier: 1.0m,
            AtrPeriod: 10, AtrPercentileLookback: 75,
            MinScore: 5,
            HtfConfirmationTimeframe: TimeFrame.H4),

        _ => new( // Swing
            SupertrendPeriod: 10, SupertrendMultiplier: 3.0m,
            EmaFast: 12, EmaSlow: 26, EmaTrendFilter: 50,
            AdxPeriod: 14, MinAdx: 15m,
            RsiPeriod: 14, RsiLongMin: 40m, RsiLongMax: 75m, RsiShortMin: 25m, RsiShortMax: 60m,
            VolumePeriod: 20, VolumeMultiplier: 1.0m,
            AtrPeriod: 14, AtrPercentileLookback: 100,
            MinScore: 6,
            HtfConfirmationTimeframe: TimeFrame.D1),
    };

    /// <summary>Risiko-Parameter pro Modus.</summary>
    public static RiskPreset GetRiskPreset(TradingModePreset mode) => mode switch
    {
        TradingModePreset.Scalping => new(
            MaxPositionSizePercent: 10m, MaxMarginPerTradePercent: 1m, MaxLeverage: 3m,
            CooldownHours: 2, MaxCooldownHours: 8,
            MaxHoldHours: 4, MaxHoldHoursAfterTp1: 8,
            Tp1CloseRatio: 0.5m, Tp2CloseRatio: 0.3m,
            SmartBreakevenAtrMultiplier: 0.3m,
            MinRiskRewardRatio: 1.2m),

        TradingModePreset.DayTrading => new(
            MaxPositionSizePercent: 15m, MaxMarginPerTradePercent: 1.5m, MaxLeverage: 3m,
            CooldownHours: 4, MaxCooldownHours: 12,
            MaxHoldHours: 24, MaxHoldHoursAfterTp1: 48,
            Tp1CloseRatio: 0.4m, Tp2CloseRatio: 0.3m,
            SmartBreakevenAtrMultiplier: 0.4m,
            MinRiskRewardRatio: 1.5m),

        _ => new( // Swing
            MaxPositionSizePercent: 20m, MaxMarginPerTradePercent: 2m, MaxLeverage: 3m,
            CooldownHours: 4, MaxCooldownHours: 24,
            MaxHoldHours: 48, MaxHoldHoursAfterTp1: 96,
            Tp1CloseRatio: 0.3m, Tp2CloseRatio: 0.3m,
            SmartBreakevenAtrMultiplier: 0.5m,
            MinRiskRewardRatio: 1.5m),
    };

    /// <summary>Scanner-Parameter pro Modus.</summary>
    public static ScannerPreset GetScannerPreset(TradingModePreset mode) => mode switch
    {
        TradingModePreset.Scalping => new(
            ScanTimeFrame: TimeFrame.M15,
            MinVolume24h: 100_000_000m, // Liquidität kritisch bei Scalping
            MinPriceChange: 0.3m,
            MaxResults: 10,
            UseM15EntryTiming: false), // Schon auf M15, kein Extra-Timing nötig

        TradingModePreset.DayTrading => new(
            ScanTimeFrame: TimeFrame.H1,
            MinVolume24h: 50_000_000m,
            MinPriceChange: 0.5m,
            MaxResults: 15,
            UseM15EntryTiming: true),

        _ => new( // Swing
            ScanTimeFrame: TimeFrame.H4,
            MinVolume24h: 20_000_000m,
            MinPriceChange: 0.5m,
            MaxResults: 20,
            UseM15EntryTiming: true),
    };

    /// <summary>Vol-adaptive SL/TP-Multiplikatoren pro Modus + ATR-Perzentil.</summary>
    public static (decimal slMult, decimal tp1Mult, decimal tp2Mult, decimal trailMult)
        GetVolAdaptiveMultipliers(TradingModePreset mode, int atrPercentile)
    {
        return mode switch
        {
            // Scalping: SL mindestens 1.5x ATR (1.0x ist zu eng für Meme-Coins mit großem Spread)
            TradingModePreset.Scalping => atrPercentile switch
            {
                < 20  => (1.5m, 2.0m, 3.0m, 1.8m),
                < 50  => (1.8m, 2.2m, 3.5m, 2.0m),
                < 75  => (2.0m, 2.5m, 4.0m, 2.2m),
                < 90  => (2.2m, 3.0m, 4.5m, 2.5m),
                _     => (1.8m, 2.0m, 3.0m, 1.8m),
            },
            TradingModePreset.DayTrading => atrPercentile switch
            {
                < 20  => (1.5m, 2.0m, 3.5m, 2.0m),
                < 50  => (1.8m, 2.5m, 4.0m, 2.2m),
                < 75  => (2.0m, 2.5m, 4.5m, 2.5m),
                < 90  => (2.2m, 3.0m, 5.0m, 2.8m),
                _     => (1.8m, 2.0m, 3.5m, 2.0m),
            },
            _ => atrPercentile switch // Swing (aktuelle Werte)
            {
                < 20  => (1.5m, 2.0m, 3.5m, 2.0m),
                < 50  => (1.8m, 2.5m, 4.5m, 2.2m),
                < 75  => (2.0m, 3.0m, 5.0m, 2.5m),
                < 90  => (2.5m, 3.5m, 6.0m, 3.0m),
                _     => (2.0m, 2.5m, 4.0m, 2.0m),
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
    int MaxResults, bool UseM15EntryTiming);
