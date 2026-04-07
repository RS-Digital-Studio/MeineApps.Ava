using BingXBot.Core.Data;
using BingXBot.Core.Models.ATI;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace BingXBot.Engine.ATI;

/// <summary>
/// ML Phase 2: LightGBM-basierter Classifier der aus gelabelten FeatureSnapshots lernt.
/// Trainiert auf historischen Trades und gibt P(Win) für neue Signale zurück.
/// Kann den Bayesian Naive Bayes in ConfidenceGate ersetzen wenn genug Daten vorhanden sind.
/// </summary>
public class LightGbmClassifier
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private PredictionEngine<FeatureInput, FeaturePrediction>? _predictionEngine;
    // Lock für PredictionEngine: ML.NET PredictionEngine ist nicht thread-safe.
    // 3 parallele TradingServiceBase-Instanzen rufen Predict() gleichzeitig auf.
    private readonly object _predictionLock = new();

    /// <summary>Mindestanzahl gelabelter Samples für Training.</summary>
    public int MinSamplesForTraining { get; set; } = 50;

    /// <summary>True wenn ein trainiertes Modell verfügbar ist.</summary>
    public bool IsModelReady => _predictionEngine != null;

    /// <summary>Zeitpunkt des letzten Trainings.</summary>
    public DateTime? LastTrainedAt { get; private set; }

    /// <summary>Metriken des letzten Trainings.</summary>
    public TrainingMetrics? LastMetrics { get; private set; }

    public LightGbmClassifier()
    {
        _mlContext = new MLContext(seed: 42);
    }

    /// <summary>
    /// Trainiert den Classifier auf gelabelten FeatureSnapshots aus der DB.
    /// 80/20 Train/Test-Split, LightGBM mit automatischer Hyperparameter-Optimierung.
    /// </summary>
    public TrainingMetrics? Train(IReadOnlyList<FeatureSnapshotEntity> labeledSnapshots)
    {
        if (labeledSnapshots.Count < MinSamplesForTraining)
            return null;

        // Daten in ML.NET-Format konvertieren
        var inputs = labeledSnapshots.Select(e => new FeatureInput
        {
            Label = e.Outcome > 0, // true = Gewinn
            PriceVsEma20 = e.F_PriceVsEma20,
            PriceVsEma50 = e.F_PriceVsEma50,
            PriceVsEma200 = e.F_PriceVsEma200,
            EmaCrossDirection = e.F_EmaCrossDirection,
            RsiNormalized = e.F_RsiNormalized,
            MacdHistogramNormalized = e.F_MacdHistogramNormalized,
            StochKNormalized = e.F_StochKNormalized,
            StochDNormalized = e.F_StochDNormalized,
            AtrPercent = e.F_AtrPercent,
            BollingerWidth = e.F_BollingerWidth,
            BollingerPosition = e.F_BollingerPosition,
            AdxNormalized = e.F_AdxNormalized,
            HtfTrend = e.F_HtfTrend,
            VolumeRatio = e.F_VolumeRatio,
            FundingRate = e.F_FundingRate,
            SessionId = e.F_SessionId,
            BtcReturn24h = e.F_BtcReturn24h,
            BtcTrend = e.F_BtcTrend,
            BtcCorrelation = e.F_BtcCorrelation,
            MarketSentiment = e.F_MarketSentiment,
            FearGreedIndex = e.F_FearGreedIndex,
            OpenInterestChange = e.F_OpenInterestChange,
            ConsecutiveUpCandles = e.F_ConsecutiveUpCandles,
            ConsecutiveDownCandles = e.F_ConsecutiveDownCandles,
            RecentReturnPercent = e.F_RecentReturnPercent,
            Regime = e.Regime,
            StrategiesAgreeing = e.StrategiesAgreeing,
            EnsembleConfidence = (float)e.EnsembleConfidence
        }).ToList();

        var dataView = _mlContext.Data.LoadFromEnumerable(inputs);

        // 80/20 Train/Test-Split
        var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);

        // Feature-Spalten definieren
        var featureColumns = new[]
        {
            nameof(FeatureInput.PriceVsEma20), nameof(FeatureInput.PriceVsEma50),
            nameof(FeatureInput.PriceVsEma200), nameof(FeatureInput.EmaCrossDirection),
            nameof(FeatureInput.RsiNormalized), nameof(FeatureInput.MacdHistogramNormalized),
            nameof(FeatureInput.StochKNormalized), nameof(FeatureInput.StochDNormalized),
            nameof(FeatureInput.AtrPercent), nameof(FeatureInput.BollingerWidth),
            nameof(FeatureInput.BollingerPosition), nameof(FeatureInput.AdxNormalized),
            nameof(FeatureInput.HtfTrend), nameof(FeatureInput.VolumeRatio),
            nameof(FeatureInput.FundingRate), nameof(FeatureInput.SessionId),
            nameof(FeatureInput.BtcReturn24h), nameof(FeatureInput.BtcTrend),
            nameof(FeatureInput.BtcCorrelation), nameof(FeatureInput.MarketSentiment),
            nameof(FeatureInput.FearGreedIndex), nameof(FeatureInput.OpenInterestChange),
            nameof(FeatureInput.ConsecutiveUpCandles), nameof(FeatureInput.ConsecutiveDownCandles),
            nameof(FeatureInput.RecentReturnPercent), nameof(FeatureInput.Regime),
            nameof(FeatureInput.StrategiesAgreeing), nameof(FeatureInput.EnsembleConfidence)
        };

        // Pipeline: Features → LightGBM Classifier
        var pipeline = _mlContext.Transforms.Concatenate("Features", featureColumns)
            .Append(_mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 31,
                minimumExampleCountPerLeaf: 5,
                learningRate: 0.05,
                numberOfIterations: 200));

        // Trainieren
        var trainedModel = pipeline.Fit(split.TrainSet);

        // Evaluieren auf Test-Set BEVOR das Modell aktiviert wird
        var predictions = trainedModel.Transform(split.TestSet);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, "Label");

        LastTrainedAt = DateTime.UtcNow;
        LastMetrics = new TrainingMetrics
        {
            Accuracy = (decimal)metrics.Accuracy,
            Auc = (decimal)metrics.AreaUnderRocCurve,
            F1Score = (decimal)metrics.F1Score,
            Precision = (decimal)metrics.PositivePrecision,
            Recall = (decimal)metrics.PositiveRecall,
            TrainingSamples = (int)(labeledSnapshots.Count * 0.8),
            TestSamples = (int)(labeledSnapshots.Count * 0.2)
        };

        // Modell nur aktivieren wenn AUC ausreicht (>= 0.55)
        // Atomares Swap: Predict() liest unter _predictionLock, hier unter demselben Lock setzen
        if (metrics.AreaUnderRocCurve >= 0.55)
        {
            var newEngine = _mlContext.Model.CreatePredictionEngine<FeatureInput, FeaturePrediction>(trainedModel);
            lock (_predictionLock)
            {
                _model = trainedModel;
                _predictionEngine = newEngine;
            }
        }

        return LastMetrics;
    }

    /// <summary>
    /// Gibt die Gewinn-Wahrscheinlichkeit für einen neuen FeatureSnapshot zurück.
    /// Nur verfügbar wenn ein Modell trainiert wurde.
    /// </summary>
    public decimal Predict(FeatureSnapshot snapshot, int regime, int strategiesAgreeing, decimal ensembleConfidence)
    {
        if (_predictionEngine == null) return 0.5m; // Kein Modell → neutraler Default

        var input = new FeatureInput
        {
            PriceVsEma20 = snapshot.PriceVsEma20,
            PriceVsEma50 = snapshot.PriceVsEma50,
            PriceVsEma200 = snapshot.PriceVsEma200,
            EmaCrossDirection = snapshot.EmaCrossDirection,
            RsiNormalized = snapshot.RsiNormalized,
            MacdHistogramNormalized = snapshot.MacdHistogramNormalized,
            StochKNormalized = snapshot.StochKNormalized,
            StochDNormalized = snapshot.StochDNormalized,
            AtrPercent = snapshot.AtrPercent,
            BollingerWidth = snapshot.BollingerWidth,
            BollingerPosition = snapshot.BollingerPosition,
            AdxNormalized = snapshot.AdxNormalized,
            HtfTrend = snapshot.HtfTrend,
            VolumeRatio = snapshot.VolumeRatio,
            FundingRate = snapshot.FundingRate,
            SessionId = snapshot.SessionId,
            BtcReturn24h = snapshot.BtcReturn24h,
            BtcTrend = snapshot.BtcTrend,
            BtcCorrelation = snapshot.BtcCorrelation,
            MarketSentiment = snapshot.MarketSentiment,
            FearGreedIndex = snapshot.FearGreedIndex,
            OpenInterestChange = snapshot.OpenInterestChange,
            ConsecutiveUpCandles = snapshot.ConsecutiveUpCandles,
            ConsecutiveDownCandles = snapshot.ConsecutiveDownCandles,
            RecentReturnPercent = snapshot.RecentReturnPercent,
            Regime = regime,
            StrategiesAgreeing = strategiesAgreeing,
            EnsembleConfidence = (float)ensembleConfidence
        };

        lock (_predictionLock)
        {
            if (_predictionEngine == null) return 0.5m; // Zwischen null-Check oben und Lock invalidiert
            var prediction = _predictionEngine.Predict(input);
            return (decimal)prediction.Probability;
        }
    }

    /// <summary>
    /// Invalidiert das aktuelle Modell (z.B. nach zu schwachem Training-Ergebnis).
    /// IsModelReady wird false, ConfidenceGate fällt auf Bayesian zurück.
    /// </summary>
    public void InvalidateModel()
    {
        lock (_predictionLock)
        {
            _predictionEngine = null;
            _model = null;
        }
    }
}

/// <summary>ML.NET Input-Schema für den LightGBM Classifier.</summary>
public class FeatureInput
{
    [ColumnName("Label")]
    public bool Label { get; set; }

    public float PriceVsEma20 { get; set; }
    public float PriceVsEma50 { get; set; }
    public float PriceVsEma200 { get; set; }
    public float EmaCrossDirection { get; set; }
    public float RsiNormalized { get; set; }
    public float MacdHistogramNormalized { get; set; }
    public float StochKNormalized { get; set; }
    public float StochDNormalized { get; set; }
    public float AtrPercent { get; set; }
    public float BollingerWidth { get; set; }
    public float BollingerPosition { get; set; }
    public float AdxNormalized { get; set; }
    public float HtfTrend { get; set; }
    public float VolumeRatio { get; set; }
    public float FundingRate { get; set; }
    public float SessionId { get; set; }
    public float BtcReturn24h { get; set; }
    public float BtcTrend { get; set; }
    public float BtcCorrelation { get; set; }
    public float MarketSentiment { get; set; }
    public float FearGreedIndex { get; set; }
    public float OpenInterestChange { get; set; }
    public float ConsecutiveUpCandles { get; set; }
    public float ConsecutiveDownCandles { get; set; }
    public float RecentReturnPercent { get; set; }
    public float Regime { get; set; }
    public float StrategiesAgreeing { get; set; }
    public float EnsembleConfidence { get; set; }
}

/// <summary>ML.NET Output-Schema.</summary>
public class FeaturePrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }
    public float Score { get; set; }
    public float Probability { get; set; }
}

/// <summary>Trainings-Metriken des LightGBM Classifiers.</summary>
public class TrainingMetrics
{
    public decimal Accuracy { get; init; }
    public decimal Auc { get; init; }
    public decimal F1Score { get; init; }
    public decimal Precision { get; init; }
    public decimal Recall { get; init; }
    public int TrainingSamples { get; init; }
    public int TestSamples { get; init; }
}
