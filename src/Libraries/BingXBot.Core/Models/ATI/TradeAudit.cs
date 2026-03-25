using BingXBot.Core.Enums;

namespace BingXBot.Core.Models.ATI;

/// <summary>
/// Audit-Trail einer Trading-Entscheidung. Enthaelt alle Informationen
/// WARUM ein Trade genommen oder abgelehnt wurde.
/// Wird fuer Analyse und Debugging gespeichert.
/// </summary>
public record TradeAudit(
    DateTime Timestamp,
    string Symbol,
    Signal SignalDirection,
    bool WasAccepted,
    // Regime
    MarketRegime Regime,
    decimal RegimeConfidence,
    // Ensemble
    int StrategiesAgreeing,
    int StrategiesTotal,
    string AgreeingStrategies,
    decimal EnsembleConfidence,
    // ML Confidence Gate
    decimal MlConfidence,
    decimal MlThreshold,
    // Exit-Parameter (falls akzeptiert)
    decimal? OptimizedStopLoss,
    decimal? OptimizedTakeProfit,
    decimal? TrailingStopPercent,
    // Ablehnungsgrund (falls abgelehnt)
    string? RejectionReason,
    // Zusammenfassung (menschenlesbar)
    string Summary);
