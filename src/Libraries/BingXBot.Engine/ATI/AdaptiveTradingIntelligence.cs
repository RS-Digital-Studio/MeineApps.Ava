using System.Collections.Concurrent;
using System.Text.Json;
using BingXBot.Core.Enums;
using BingXBot.Core.Interfaces;
using BingXBot.Core.Models;
using BingXBot.Core.Models.ATI;
using BingXBot.Engine.Indicators;

namespace BingXBot.Engine.ATI;

/// <summary>
/// Adaptive Trading Intelligence - Hauptorchestrator.
/// Verbindet alle ATI-Komponenten zu einer integrierten Entscheidungspipeline:
/// Features → Regime → Ensemble → Confidence Gate → Exit-Optimierung → Audit-Trail.
/// </summary>
public class AdaptiveTradingIntelligence
{
    private readonly RegimeDetector _regimeDetector;
    private readonly AdaptiveEnsemble _ensemble;
    private readonly ConfidenceGate _confidenceGate;
    private readonly ExitOptimizer _exitOptimizer;

    // Feature-Snapshots offener Trades: Key = "Symbol_Side" → (FeatureSnapshot, RegimeState, EnsembleVote)
    // Wird beim Trade-Close gebraucht um Outcome den ursprünglichen Features zuzuordnen
    private readonly ConcurrentDictionary<string, TradeContext> _openTradeContexts = new();

    /// <summary>Ob ATI aktiviert ist (kann im UI deaktiviert werden).</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Event: Wird ausgelöst wenn ein Audit-Trail erstellt wurde (für Logging/UI).</summary>
    public event Action<TradeAudit>? AuditCreated;

    // Ablehnungs-Zähler pro Scan-Zyklus (für Debug-Zusammenfassung)
    private int _rejectedChaotic;
    private int _rejectedNoConsensus;
    private int _rejectedLowConfidence;
    private int _accepted;

    /// <summary>
    /// Setzt die Scan-Zähler zurück und gibt eine Zusammenfassung zurück (für Debug-Logging).
    /// Aufrufen am Ende jedes Scan-Zyklus.
    /// </summary>
    public string? GetScanSummaryAndReset()
    {
        var chaotic = Interlocked.Exchange(ref _rejectedChaotic, 0);
        var noConsensus = Interlocked.Exchange(ref _rejectedNoConsensus, 0);
        var lowConf = Interlocked.Exchange(ref _rejectedLowConfidence, 0);
        var acc = Interlocked.Exchange(ref _accepted, 0);
        var total = chaotic + noConsensus + lowConf + acc;

        if (total == 0) return null;
        if (acc > 0) return null; // Wenn Trades akzeptiert wurden, ist kein Debug-Log nötig

        var parts = new List<string>();
        if (chaotic > 0) parts.Add($"{chaotic}× Regime chaotisch");
        if (noConsensus > 0) parts.Add($"{noConsensus}× kein Konsens");
        if (lowConf > 0) parts.Add($"{lowConf}× ML-Confidence zu niedrig");

        return $"{total} Kandidaten evaluiert, alle abgelehnt: {string.Join(", ", parts)}";
    }

    /// <summary>Aktuelle Funding Rate (wird extern gesetzt, z.B. von TradingServiceBase).</summary>
    public float CurrentFundingRate { get; set; }

    public AdaptiveTradingIntelligence()
    {
        _regimeDetector = new RegimeDetector();
        _ensemble = new AdaptiveEnsemble();
        _confidenceGate = new ConfidenceGate();
        _exitOptimizer = new ExitOptimizer();
    }

    // === Komponenten-Zugriff (für UI/Debug) ===
    public RegimeDetector RegimeDetector => _regimeDetector;
    public AdaptiveEnsemble Ensemble => _ensemble;
    public ConfidenceGate ConfidenceGate => _confidenceGate;
    public ExitOptimizer ExitOptimizer => _exitOptimizer;

    /// <summary>
    /// Registriert Strategien im Ensemble. Sollte beim Bot-Start aufgerufen werden.
    /// </summary>
    public void RegisterStrategies(IEnumerable<IStrategy> strategies)
    {
        _ensemble.ClearStrategies();
        foreach (var strategy in strategies)
            _ensemble.RegisterStrategy(strategy);
    }

    /// <summary>
    /// Vollständige ATI-Pipeline: Evaluiert einen Kandidaten und gibt ein optimiertes Signal zurück.
    /// Gibt null zurück wenn der Trade abgelehnt wird.
    /// </summary>
    public AtiResult? EvaluateCandidate(MarketContext context)
    {
        if (!IsEnabled)
            return null;

        // 1. Features extrahieren
        var features = FeatureEngine.Extract(context, CurrentFundingRate);

        // 2. Regime erkennen
        var regimeState = _regimeDetector.Detect(features);
        features.Regime = regimeState.CurrentRegime;

        // 3. Chaotisches Regime → nicht traden
        if (regimeState.CurrentRegime == MarketRegime.Chaotic && regimeState.Confidence > 0.6m)
        {
            Interlocked.Increment(ref _rejectedChaotic);
            CreateAudit(features, regimeState, null, 0m, false,
                "Chaotisches Regime erkannt - Trading pausiert");
            return null;
        }

        // 4. Ensemble evaluieren (alle Strategien)
        var ensembleVote = _ensemble.Evaluate(context, regimeState.CurrentRegime);

        // 5. Kein Konsens → kein Trade
        if (ensembleVote.ConsensusSignal == Signal.None)
        {
            Interlocked.Increment(ref _rejectedNoConsensus);
            CreateAudit(features, regimeState, ensembleVote, 0m, false,
                $"Kein Konsens: {ensembleVote.AgreeingCount}/{ensembleVote.TotalCount} Strategien");
            return null;
        }

        // 5b. Close-Signale direkt durchleiten (kein Confidence Gate / ExitOptimizer nötig)
        if (ensembleVote.ConsensusSignal is Signal.CloseLong or Signal.CloseShort)
        {
            var closeReason = $"ATI Close: Regime={regimeState.CurrentRegime}, " +
                              $"{ensembleVote.AgreeingCount}/{ensembleVote.TotalCount} ({ensembleVote.AgreeingNames})";
            var closeSignal = new SignalResult(
                ensembleVote.ConsensusSignal,
                ensembleVote.WeightedConfidence,
                null, null, null, closeReason);

            CreateAudit(features, regimeState, ensembleVote, 0m, true, closeReason);

            return new AtiResult(closeSignal, features, regimeState, ensembleVote, 0m, 0m);
        }

        // 6. Confidence Gate (ML-basierter Filter)
        var (mlConfidence, shouldTrade) = _confidenceGate.Evaluate(
            features, regimeState.CurrentRegime, ensembleVote);

        if (!shouldTrade)
        {
            Interlocked.Increment(ref _rejectedLowConfidence);
            CreateAudit(features, regimeState, ensembleVote, mlConfidence, false,
                $"ML-Confidence {mlConfidence:P0} unter Schwelle {_confidenceGate.Threshold:P0}");
            return null;
        }

        // 7. Exit-Parameter optimieren
        var atrValues = IndicatorHelper.CalculateAtr(context.Candles);
        var atr = atrValues.Count > 0 && atrValues[^1].HasValue ? atrValues[^1]!.Value : 0m;
        var entryPrice = context.CurrentTicker.LastPrice;

        var (optimizedSl, optimizedTp, trailingPercent) = _exitOptimizer.OptimizeExit(
            features, regimeState.CurrentRegime, ensembleVote.ConsensusSignal,
            atr, entryPrice, ensembleVote.WeightedConfidence);

        // 8. SL/TP Sicherheits-Fallbacks: Signal MUSS immer einen gültigen SL haben
        //    Ohne SL berechnet der RiskManager per Fallback-Sizing → absurd große Positionen bei Micro-Cap Tokens
        var finalSl = optimizedSl ?? ensembleVote.BestStopLoss;
        var finalTp = optimizedTp ?? ensembleVote.BestTakeProfit;

        if (!finalSl.HasValue && atr > 0)
        {
            // Fallback: 2x ATR als Stop-Loss
            finalSl = ensembleVote.ConsensusSignal == Signal.Long
                ? entryPrice - atr * 2m
                : entryPrice + atr * 2m;
        }
        if (!finalSl.HasValue && entryPrice > 0)
        {
            // Letzter Fallback: 3% Stop-Loss (wenn ATR auch 0 ist)
            finalSl = ensembleVote.ConsensusSignal == Signal.Long
                ? entryPrice * 0.97m
                : entryPrice * 1.03m;
        }

        if (!finalTp.HasValue && atr > 0)
        {
            // Fallback: 4x ATR als Take-Profit
            finalTp = ensembleVote.ConsensusSignal == Signal.Long
                ? entryPrice + atr * 4m
                : entryPrice - atr * 4m;
        }
        if (!finalTp.HasValue && entryPrice > 0)
        {
            // Letzter Fallback: 6% Take-Profit
            finalTp = ensembleVote.ConsensusSignal == Signal.Long
                ? entryPrice * 1.06m
                : entryPrice * 0.94m;
        }

        Interlocked.Increment(ref _accepted);

        // 9. SignalResult zusammenbauen
        var combinedConfidence = (ensembleVote.WeightedConfidence + mlConfidence) / 2m;
        var reason = $"ATI: Regime={regimeState.CurrentRegime}, " +
                     $"Ensemble={ensembleVote.AgreeingCount}/{ensembleVote.TotalCount} ({ensembleVote.AgreeingNames}), " +
                     $"ML={mlConfidence:P0}";

        var optimizedSignal = new SignalResult(
            ensembleVote.ConsensusSignal,
            combinedConfidence,
            entryPrice,
            finalSl,
            finalTp,
            reason);

        // 10. Audit-Trail erstellen
        CreateAudit(features, regimeState, ensembleVote, mlConfidence, true, reason,
            finalSl, finalTp, trailingPercent);

        return new AtiResult(optimizedSignal, features, regimeState, ensembleVote,
            mlConfidence, trailingPercent);
    }

    /// <summary>
    /// Speichert den Kontext eines eröffneten Trades (für späteres Lernen beim Close).
    /// Aufrufen NACH erfolgreicher Order-Platzierung.
    /// </summary>
    public void RegisterOpenTrade(string symbol, Side side, FeatureSnapshot features,
        RegimeState regime, EnsembleVote ensembleVote, decimal slMultiplier, decimal tpMultiplier)
    {
        var key = $"{symbol}_{side}";
        _openTradeContexts[key] = new TradeContext(features, regime, ensembleVote,
            slMultiplier, tpMultiplier, DateTime.UtcNow);
    }

    /// <summary>
    /// Verarbeitet ein Trade-Ergebnis und aktualisiert alle lernenden Komponenten.
    /// Aufrufen wenn ein Trade geschlossen wird.
    /// </summary>
    public void ProcessTradeOutcome(CompletedTrade trade)
    {
        var key = $"{trade.Symbol}_{trade.Side}";
        if (!_openTradeContexts.TryRemove(key, out var ctx))
            return; // Kein gespeicherter Kontext (Trade wurde vor ATI eröffnet oder manuell)

        var won = trade.Pnl > 0;

        // 1. Ensemble-Gewichte aktualisieren (pro Strategie)
        if (ctx.EnsembleVote.Votes != null)
        {
            foreach (var vote in ctx.EnsembleVote.Votes)
            {
                if (vote.Signal == ctx.EnsembleVote.ConsensusSignal)
                {
                    _ensemble.RecordOutcome(vote.StrategyName, ctx.Regime.CurrentRegime, won);
                }
            }
        }

        // 2. Confidence Gate aktualisieren (Bayesian Update)
        _confidenceGate.RecordOutcome(ctx.Features, ctx.Regime.CurrentRegime, ctx.EnsembleVote, won);

        // 3. Exit-Optimizer aktualisieren
        var holdTimeMinutes = (trade.ExitTime - trade.EntryTime).TotalMinutes;
        var pnlPercent = trade.EntryPrice > 0 ? trade.Pnl / (trade.EntryPrice * trade.Quantity) : 0m;
        _exitOptimizer.RecordExitOutcome(ctx.Regime.CurrentRegime, ctx.EnsembleVote.WeightedConfidence,
            ctx.SlMultiplier, ctx.TpMultiplier, won, pnlPercent);
    }

    /// <summary>Serialisiert den gesamten ATI-Lernzustand als JSON.</summary>
    public string SerializeState()
    {
        var state = new AtiPersistedState
        {
            ConfidenceGateBuckets = _confidenceGate.SerializeState(),
            EnsembleWeights = _ensemble.SerializeState(),
            ExitOptimizerStats = _exitOptimizer.SerializeState(),
            RegimeTransitions = _regimeDetector.SerializeState(),
            SavedAt = DateTime.UtcNow
        };
        return JsonSerializer.Serialize(state);
    }

    /// <summary>Lädt einen gespeicherten ATI-Lernzustand aus JSON.</summary>
    public void DeserializeState(string json)
    {
        try
        {
            var state = JsonSerializer.Deserialize<AtiPersistedState>(json);
            if (state == null) return;

            _confidenceGate.DeserializeState(state.ConfidenceGateBuckets);
            _ensemble.DeserializeState(state.EnsembleWeights);
            _exitOptimizer.DeserializeState(state.ExitOptimizerStats);
            _regimeDetector.DeserializeState(state.RegimeTransitions);
        }
        catch { /* Korrupte Daten → ignorieren, mit leeren Modellen starten */ }
    }

    /// <summary>Setzt alle lernenden Komponenten zurück.</summary>
    public void Reset()
    {
        _confidenceGate.Reset();
        _openTradeContexts.Clear();
    }

    // === Interne Methoden ===

    private void CreateAudit(FeatureSnapshot features, RegimeState regime,
        EnsembleVote? ensemble, decimal mlConfidence, bool accepted, string summary,
        decimal? sl = null, decimal? tp = null, decimal? trailingPercent = null)
    {
        var audit = new TradeAudit(
            DateTime.UtcNow,
            features.Symbol,
            ensemble?.ConsensusSignal ?? Signal.None,
            accepted,
            regime.CurrentRegime,
            regime.Confidence,
            ensemble?.AgreeingCount ?? 0,
            ensemble?.TotalCount ?? 0,
            ensemble?.AgreeingNames ?? "",
            ensemble?.WeightedConfidence ?? 0m,
            mlConfidence,
            _confidenceGate.Threshold,
            sl, tp, trailingPercent,
            accepted ? null : summary,
            summary);

        AuditCreated?.Invoke(audit);
    }

    // === Verschachtelte Typen ===

    /// <summary>Persistierter ATI-Lernzustand (JSON-serialisierbar).</summary>
    public class AtiPersistedState
    {
        public string ConfidenceGateBuckets { get; set; } = "";
        public string EnsembleWeights { get; set; } = "";
        public string ExitOptimizerStats { get; set; } = "";
        public string RegimeTransitions { get; set; } = "";
        public DateTime SavedAt { get; set; }
    }

    /// <summary>Gespeicherter Kontext eines offenen Trades (für Lernen beim Close).</summary>
    private record TradeContext(
        FeatureSnapshot Features,
        RegimeState Regime,
        EnsembleVote EnsembleVote,
        decimal SlMultiplier,
        decimal TpMultiplier,
        DateTime OpenedAt);
}

/// <summary>Ergebnis der ATI-Pipeline (optimiertes Signal + Kontext).</summary>
public record AtiResult(
    SignalResult Signal,
    FeatureSnapshot Features,
    RegimeState Regime,
    EnsembleVote EnsembleVote,
    decimal MlConfidence,
    decimal TrailingStopPercent);
