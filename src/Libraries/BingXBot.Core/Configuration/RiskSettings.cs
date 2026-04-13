using BingXBot.Core.Enums;

namespace BingXBot.Core.Configuration;

/// <summary>Marktspezifische Risk-Overrides. Fehlende Kategorien nutzen globale RiskSettings-Defaults.</summary>
public record MarketCategorySettings
{
    public decimal MaxLeverage { get; init; } = 3m;
    public decimal MaxPositionSizePercent { get; init; } = 3m;
    /// <summary>SK-Buch S.13: 1% Risiko pro Trade (1-3% Tagesrisiko, bei mehreren Trades aufteilen).</summary>
    public decimal MaxMarginPerTradePercent { get; init; } = 1m;
    /// <summary>SK-Buch S.13: min CRV 1:1.</summary>
    public decimal MinRiskRewardRatio { get; init; } = 1.0m;
}

/// <summary>
/// Risk-Settings strikt nach SK-Buch (Tradebook Sascha Wenzel / Stefan Kassing).
/// Alle Non-Buch-Features (Trailing, Chandelier, Momentum-Decay, Equity-Curve-Trading,
/// Funding-Rate-Filter, Cooldown-Eskalation, Netto-Exposure, Adaptive-Leverage) entfernt.
/// </summary>
public class RiskSettings
{
    /// <summary>Max. Margin pro Trade in % der Balance. SK-Buch: 1-3%, konservativ 1%.</summary>
    public decimal MaxPositionSizePercent { get; set; } = 3m;
    /// <summary>Max. Risiko (Verlust bei SL-Hit) pro Trade in % der Balance. SK-Buch S.13: 1%.</summary>
    public decimal MaxMarginPerTradePercent { get; set; } = 1m;
    /// <summary>Max. Tages-Drawdown in %. 0 = deaktiviert.</summary>
    public decimal MaxDailyDrawdownPercent { get; set; } = 0m;
    /// <summary>Max. Total-Drawdown in % (Safety-Net gegen Bot-Crash).</summary>
    public decimal MaxTotalDrawdownPercent { get; set; } = 10m;
    /// <summary>Max. gleichzeitig offene Positionen. SK-Buch Workflow: max. 3 Trades parallel.</summary>
    public int MaxOpenPositions { get; set; } = 3;
    public int MaxOpenPositionsPerSymbol { get; set; } = 1;

    /// <summary>Max. Leverage-Faktor.</summary>
    public decimal MaxLeverage { get; set; } = 10m;

    /// <summary>Risiko-Diversifikation (Buch S.19): Korrelations-Check gegen bestehende Positionen.</summary>
    public bool CheckCorrelation { get; set; } = true;
    public decimal MaxCorrelation { get; set; } = 0.85m;

    // === Partial Close (TP1 + TP2) — 50/50-Staffelung aus Buch S.16 Zielbereich 161.8-200% ===
    /// <summary>Anteil der Position bei TP1 (161.8% Extension) geschlossen. SK: 50%.</summary>
    public decimal Tp1CloseRatio { get; set; } = 0.5m;
    /// <summary>Anteil der Position bei TP2 (200% + 20 Pips Buffer) geschlossen. SK: 50% Rest.</summary>
    public decimal Tp2CloseRatio { get; set; } = 0.5m;

    /// <summary>Max. Haltezeit in Stunden (0 = unbegrenzt — SL/TP managed Exit, Buch-konform).</summary>
    public int MaxHoldHours { get; set; } = 0;

    /// <summary>
    /// Minimales RRR. SK-Buch S.13: min 1:1. Strategy hat eigenen Check auf 1:1.
    /// 0 = deaktiviert (Strategy-Check genügt).
    /// </summary>
    public decimal MinRiskRewardRatio { get; set; } = 0m;

    /// <summary>Basis-Cooldown nach SL-Hit in Stunden (0 = deaktiviert, Buch 6.8: nach BE sofort Re-Entry).</summary>
    public int CooldownHours { get; set; } = 0;
    /// <summary>Max. Trades pro Tag (0 = unbegrenzt).</summary>
    public int MaxTradesPerDay { get; set; } = 0;

    /// <summary>Mindest-Abstand zum Liquidationspreis in % (Safety-Net, nicht im Buch aber sinnvoll).</summary>
    public decimal MinLiquidationDistancePercent { get; set; } = 3m;

    // === Per-Markt Risk-Overrides ===
    /// <summary>Marktspezifische Risk-Settings. SK-Buch: einheitlich 1% Risiko für alle Kategorien.</summary>
    public Dictionary<MarketCategory, MarketCategorySettings> CategorySettings { get; set; } = new()
    {
        { MarketCategory.Crypto,    new() { MaxLeverage = 5m,  MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Commodity, new() { MaxLeverage = 10m, MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Index,     new() { MaxLeverage = 10m, MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Forex,     new() { MaxLeverage = 20m, MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
        { MarketCategory.Stock,     new() { MaxLeverage = 5m,  MaxPositionSizePercent = 3m, MaxMarginPerTradePercent = 1m, MinRiskRewardRatio = 1.0m } },
    };

    public MarketCategorySettings GetCategorySettings(MarketCategory category)
        => CategorySettings.GetValueOrDefault(category, new MarketCategorySettings
        {
            MaxLeverage = MaxLeverage,
            MaxPositionSizePercent = MaxPositionSizePercent,
            MaxMarginPerTradePercent = MaxMarginPerTradePercent,
            MinRiskRewardRatio = MinRiskRewardRatio
        });
}
