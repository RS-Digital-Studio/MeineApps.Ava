using BingXBot.Core.Models.ATI;
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

    // Alle 26 Features als einzelne Spalten (schneller als JSON-Blob fuer Queries)
    // Preis
    public float F_PriceVsEma20 { get; set; }
    public float F_PriceVsEma50 { get; set; }
    public float F_PriceVsEma200 { get; set; }
    public float F_EmaCrossDirection { get; set; }
    // Momentum
    public float F_RsiNormalized { get; set; }
    public float F_MacdHistogramNormalized { get; set; }
    public float F_StochKNormalized { get; set; }
    public float F_StochDNormalized { get; set; }
    // Volatilität
    public float F_AtrPercent { get; set; }
    public float F_BollingerWidth { get; set; }
    public float F_BollingerPosition { get; set; }
    // Trend
    public float F_AdxNormalized { get; set; }
    public float F_HtfTrend { get; set; }
    // Volumen
    public float F_VolumeRatio { get; set; }
    // Markt
    public float F_FundingRate { get; set; }
    public float F_SessionId { get; set; }
    // Pattern
    public float F_ConsecutiveUpCandles { get; set; }
    public float F_ConsecutiveDownCandles { get; set; }
    public float F_RecentReturnPercent { get; set; }
    // Cross-Market (BTC-Kontext)
    public float F_BtcReturn24h { get; set; }
    public float F_BtcTrend { get; set; }
    public float F_BtcCorrelation { get; set; }
    public float F_MarketSentiment { get; set; }
    public float F_FearGreedIndex { get; set; }
    // Derivatives
    public float F_OpenInterestChange { get; set; }
    // Fibonacci
    public float F_FibProximity { get; set; }

    /// <summary>Erstellt eine Entity aus einem FeatureSnapshot (für DB-Persistenz).</summary>
    public static FeatureSnapshotEntity FromSnapshot(FeatureSnapshot snapshot, int signalDirection, int strategiesAgreeing, int strategiesTotal, decimal ensembleConfidence)
    {
        return new FeatureSnapshotEntity
        {
            Symbol = snapshot.Symbol,
            Timestamp = snapshot.Timestamp,
            Regime = (int)snapshot.Regime,
            SignalDirection = signalDirection,
            StrategiesAgreeing = strategiesAgreeing,
            StrategiesTotal = strategiesTotal,
            EnsembleConfidence = ensembleConfidence,
            // Alle 23 Features
            F_PriceVsEma20 = snapshot.PriceVsEma20,
            F_PriceVsEma50 = snapshot.PriceVsEma50,
            F_PriceVsEma200 = snapshot.PriceVsEma200,
            F_EmaCrossDirection = snapshot.EmaCrossDirection,
            F_RsiNormalized = snapshot.RsiNormalized,
            F_MacdHistogramNormalized = snapshot.MacdHistogramNormalized,
            F_StochKNormalized = snapshot.StochKNormalized,
            F_StochDNormalized = snapshot.StochDNormalized,
            F_AtrPercent = snapshot.AtrPercent,
            F_BollingerWidth = snapshot.BollingerWidth,
            F_BollingerPosition = snapshot.BollingerPosition,
            F_AdxNormalized = snapshot.AdxNormalized,
            F_HtfTrend = snapshot.HtfTrend,
            F_VolumeRatio = snapshot.VolumeRatio,
            F_FundingRate = snapshot.FundingRate,
            F_SessionId = snapshot.SessionId,
            F_ConsecutiveUpCandles = snapshot.ConsecutiveUpCandles,
            F_ConsecutiveDownCandles = snapshot.ConsecutiveDownCandles,
            F_RecentReturnPercent = snapshot.RecentReturnPercent,
            F_BtcReturn24h = snapshot.BtcReturn24h,
            F_BtcTrend = snapshot.BtcTrend,
            F_BtcCorrelation = snapshot.BtcCorrelation,
            F_MarketSentiment = snapshot.MarketSentiment,
            F_FearGreedIndex = snapshot.FearGreedIndex,
            F_OpenInterestChange = snapshot.OpenInterestChange,
            F_FibProximity = snapshot.FibProximity,
        };
    }
}
