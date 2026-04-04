namespace BingXBot.Core.Configuration;

public class RiskSettings
{
    public decimal MaxPositionSizePercent { get; set; } = 1.5m;
    public decimal MaxDailyDrawdownPercent { get; set; } = 3m;
    public decimal MaxTotalDrawdownPercent { get; set; } = 10m;
    public int MaxOpenPositions { get; set; } = 3;
    public int MaxOpenPositionsPerSymbol { get; set; } = 1;
    public decimal MaxLeverage { get; set; } = 3m;
    public bool CheckCorrelation { get; set; } = true;
    public decimal MaxCorrelation { get; set; } = 0.7m;
    public bool EnableTrailingStop { get; set; } = true;
    public decimal TrailingStopPercent { get; set; } = 2.5m;

    // === Multi-Stage Exit ===
    /// <summary>Ob Multi-Stage Exit aktiv ist (TP1→BE→Trailing→TP2).</summary>
    public bool EnableMultiStageExit { get; set; } = true;
    /// <summary>Anteil der Position der bei TP1 geschlossen wird (0.5 = 50%).</summary>
    public decimal Tp1CloseRatio { get; set; } = 0.5m;
    /// <summary>Max. Haltezeit in Stunden bevor Position geschlossen wird (0 = unbegrenzt).</summary>
    public int MaxHoldHours { get; set; } = 48;
    /// <summary>Verlängerte Haltezeit nach TP1 in Stunden.</summary>
    public int MaxHoldHoursAfterTp1 { get; set; } = 96;

    // === Cooldown ===
    /// <summary>Cooldown nach Verlust-Trade in Stunden.</summary>
    public int CooldownHours { get; set; } = 8;
    /// <summary>Max. Trades pro Tag (0 = unbegrenzt).</summary>
    public int MaxTradesPerDay { get; set; } = 3;

    // === Liquidation-Schutz ===
    /// <summary>Mindestabstand zum Liquidationspreis in %. Unter diesem Abstand wird kein Trade eröffnet.</summary>
    public decimal MinLiquidationDistancePercent { get; set; } = 10m;

    // === Netto-Exposure ===
    /// <summary>Max. Netto-Exposure aller offenen Positionen in % der Balance (Summe aller notional Positionswerte, NICHT Margin).</summary>
    public decimal MaxNetExposurePercent { get; set; } = 300m;

    // === Funding-Rate ===
    /// <summary>Ob Funding-Rate bei Trade-Entscheidungen berücksichtigt werden soll.</summary>
    public bool ConsiderFundingRate { get; set; } = true;
    /// <summary>Max. Funding-Rate in % gegen die Positionsrichtung. Darüber wird kein Trade eröffnet.</summary>
    public decimal MaxAdverseFundingRatePercent { get; set; } = 0.1m;
}
