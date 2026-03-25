using SQLite;

namespace BingXBot.Core.Data;

/// <summary>
/// DB-Entity fuer Feature-Snapshots: Speichert den Marktzustand zum Zeitpunkt eines Signals
/// zusammen mit dem Trade-Outcome fuer ML-Training.
/// </summary>
[Table("FeatureSnapshots")]
public class FeatureSnapshotEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // Metadaten
    public string Symbol { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public int Regime { get; set; }
    public int SignalDirection { get; set; } // Signal enum

    // Trade-Outcome (Label fuer ML)
    /// <summary>1 = Gewinn, -1 = Verlust, 0 = noch offen/unbekannt</summary>
    public int Outcome { get; set; }
    /// <summary>Tatsaechlicher PnL des Trades (0 wenn noch unbekannt).</summary>
    public decimal Pnl { get; set; }
    /// <summary>Haltedauer in Minuten (0 wenn noch unbekannt).</summary>
    public int HoldTimeMinutes { get; set; }

    // Ensemble-Info
    public int StrategiesAgreeing { get; set; }
    public int StrategiesTotal { get; set; }
    public decimal EnsembleConfidence { get; set; }

    // Alle 19 Features als einzelne Spalten (schneller als JSON-Blob fuer Queries)
    public float F_PriceVsEma20 { get; set; }
    public float F_PriceVsEma50 { get; set; }
    public float F_PriceVsEma200 { get; set; }
    public float F_EmaCrossDirection { get; set; }
    public float F_RsiNormalized { get; set; }
    public float F_MacdHistogramNormalized { get; set; }
    public float F_StochKNormalized { get; set; }
    public float F_StochDNormalized { get; set; }
    public float F_AtrPercent { get; set; }
    public float F_BollingerWidth { get; set; }
    public float F_BollingerPosition { get; set; }
    public float F_AdxNormalized { get; set; }
    public float F_HtfTrend { get; set; }
    public float F_VolumeRatio { get; set; }
    public float F_FundingRate { get; set; }
    public float F_SessionId { get; set; }
    public float F_ConsecutiveUpCandles { get; set; }
    public float F_ConsecutiveDownCandles { get; set; }
    public float F_RecentReturnPercent { get; set; }
}
