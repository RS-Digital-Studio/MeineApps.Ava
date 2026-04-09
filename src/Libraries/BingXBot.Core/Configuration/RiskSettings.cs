using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

/// <summary>Marktspezifische Risk-Overrides. Fehlende Kategorien nutzen globale RiskSettings-Defaults.</summary>
public record MarketCategorySettings
{
    public decimal MaxLeverage { get; init; } = 3m;
    public decimal MaxPositionSizePercent { get; init; } = 20m;
    public decimal MaxMarginPerTradePercent { get; init; } = 2m;
    public decimal MinRiskRewardRatio { get; init; } = 1.5m;
}

public class RiskSettings
{
    /// <summary>Max. Margin pro Trade in % der Balance. Beispiel: 10% bei 100 USDT = max 10 USDT Margin pro Position.</summary>
    public decimal MaxPositionSizePercent { get; set; } = 10m;
    /// <summary>Max. Risiko (Verlust bei SL-Hit) pro Trade in % der Balance. Beispiel: 2% bei 100 USDT = max 2 USDT Verlust wenn SL greift.</summary>
    public decimal MaxMarginPerTradePercent { get; set; } = 2m;
    public decimal MaxDailyDrawdownPercent { get; set; } = 0m; // 0 = deaktiviert
    public decimal MaxTotalDrawdownPercent { get; set; } = 10m;
    public int MaxOpenPositions { get; set; } = 10;
    public int MaxOpenPositionsPerSymbol { get; set; } = 1;
    public decimal MaxLeverage { get; set; } = 25m;
    /// <summary>Ob der Leverage automatisch an Volatilität und Signal-Stärke angepasst wird. Aus = immer MaxLeverage.</summary>
    public bool UseAdaptiveLeverage { get; set; } = true;
    public bool CheckCorrelation { get; set; } = true;
    public decimal MaxCorrelation { get; set; } = 0.7m;
    public bool EnableTrailingStop { get; set; } = true;
    public decimal TrailingStopPercent { get; set; } = 2.5m;

    // === Multi-Stage Exit ===
    /// <summary>Ob Multi-Stage Exit aktiv ist (TP1→BE→Trailing→TP2).</summary>
    public bool EnableMultiStageExit { get; set; } = true;
    /// <summary>Anteil der Position der bei TP1 geschlossen wird (0.3 = 30%). Pyramid: TP1=30%, TP2=30%, Trailing=40%.</summary>
    public decimal Tp1CloseRatio { get; set; } = 0.3m;
    /// <summary>Anteil der Position der bei TP2 geschlossen wird (0.3 = 30%). Rest bleibt für Trailing.</summary>
    public decimal Tp2CloseRatio { get; set; } = 0.3m;
    /// <summary>Max. Haltezeit in Stunden bevor Position geschlossen wird (0 = unbegrenzt).</summary>
    public int MaxHoldHours { get; set; } = 48;
    /// <summary>Verlängerte Haltezeit nach TP1 in Stunden.</summary>
    public int MaxHoldHoursAfterTp1 { get; set; } = 96;
    /// <summary>ATR-Multiplikator für Smart Breakeven nach TP1 (SL = Entry + X*ATR statt Entry exakt). 0 = klassisches BE.</summary>
    public decimal SmartBreakevenAtrMultiplier { get; set; } = 0.5m;

    // === Risk-Reward-Ratio ===
    /// <summary>Minimales Risiko-Ertrags-Verhältnis (TP/SL-Distanz). Trades unter diesem Wert werden abgelehnt. 0 = deaktiviert.</summary>
    public decimal MinRiskRewardRatio { get; set; } = 1.0m; // 1.0: Multi-Stage Exit (30/30/40) gleicht niedrige initiale RRR aus

    // === Cooldown-Eskalation ===
    /// <summary>Basis-Cooldown nach Verlust-Trade in Stunden (1 H4-Candle Pause).</summary>
    public int CooldownHours { get; set; } = 0; // 0 = deaktiviert
    /// <summary>Max. Trades pro Tag (0 = unbegrenzt). Nicht als Filter aktiv, nur für Statistik.</summary>
    public int MaxTradesPerDay { get; set; } = 0;
    /// <summary>Ob progressive Cooldown-Eskalation aktiv ist (1 Verlust=4h, 2=8h, 3+=12h + halber Leverage).</summary>
    public bool EnableCooldownEscalation { get; set; } = false;
    /// <summary>Max. Cooldown in Stunden bei Eskalation (Cap).</summary>
    public int MaxCooldownHours { get; set; } = 0;

    // === Equity-Curve-Trading ===
    /// <summary>Ob Equity-Curve-Trading aktiv ist (Position reduzieren wenn Equity unter EMA).</summary>
    public bool EnableEquityCurveTrading { get; set; } = true;
    /// <summary>Periode für die Equity-EMA (in Anzahl Trades). Schnellere Reaktion bei wenig Trades.</summary>
    public int EquityCurvePeriod { get; set; } = 10;

    // === Momentum-Decay ===
    /// <summary>Ob Momentum-Decay-Detection aktiv ist (Position reduzieren bei schrumpfendem MACD-Histogramm).</summary>
    public bool EnableMomentumDecay { get; set; } = true;

    // === Liquidation-Schutz ===
    /// <summary>Mindestabstand zum Liquidationspreis in %. Nur Sicherheitsnetz - SL schützt primär. Bei hohem Leverage (10x) ist der Abstand mathematisch ~9.6%.</summary>
    public decimal MinLiquidationDistancePercent { get; set; } = 3m;

    // === Netto-Exposure ===
    /// <summary>Max. Netto-Exposure aller offenen Positionen in % der Balance (Summe aller notional Positionswerte, NICHT Margin).</summary>
    public decimal MaxNetExposurePercent { get; set; } = 200m;

    // === Funding-Rate ===
    /// <summary>Ob Funding-Rate bei Trade-Entscheidungen berücksichtigt werden soll.</summary>
    public bool ConsiderFundingRate { get; set; } = false;
    /// <summary>Max. Funding-Rate in % gegen die Positionsrichtung. Darüber wird kein Trade eröffnet.</summary>
    public decimal MaxAdverseFundingRatePercent { get; set; } = 0.1m;

    // === Per-Markt Risk-Overrides ===
    /// <summary>
    /// Marktspezifische Leverage- und Margin-Settings. Fehlende Kategorien nutzen globale Defaults.
    /// Krypto: Konservativ (3x). Forex: Hoch (20x, Standard bei Forex-Brokern). Commodities/Indices: Mittel (10x). Stocks: Konservativ (3x).
    /// </summary>
    public Dictionary<MarketCategory, MarketCategorySettings> CategorySettings { get; set; } = new()
    {
        { MarketCategory.Crypto,    new() { MaxLeverage = 8m,  MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 2m, MinRiskRewardRatio = 1.5m } },
        { MarketCategory.Commodity, new() { MaxLeverage = 10m, MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 2m, MinRiskRewardRatio = 1.5m } },
        { MarketCategory.Index,     new() { MaxLeverage = 10m, MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 2m, MinRiskRewardRatio = 1.5m } },
        { MarketCategory.Forex,     new() { MaxLeverage = 20m, MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 2m, MinRiskRewardRatio = 1.5m } },
        { MarketCategory.Stock,     new() { MaxLeverage = 5m,  MaxPositionSizePercent = 10m, MaxMarginPerTradePercent = 2m, MinRiskRewardRatio = 1.5m } },
    };

    /// <summary>Gibt MarketCategorySettings für eine Kategorie zurück (Fallback: globale Defaults).</summary>
    public MarketCategorySettings GetCategorySettings(MarketCategory category)
        => CategorySettings.GetValueOrDefault(category, new MarketCategorySettings
        {
            MaxLeverage = MaxLeverage,
            MaxPositionSizePercent = MaxPositionSizePercent,
            MaxMarginPerTradePercent = MaxMarginPerTradePercent,
            MinRiskRewardRatio = MinRiskRewardRatio
        });
}
