using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

public record SignalResult(
    Signal Signal,
    decimal Confidence,
    decimal? EntryPrice,
    decimal? StopLoss,
    decimal? TakeProfit,
    string Reason,
    decimal? TakeProfit2 = null,
    int ConfluenceScore = 0,
    /// <summary>SK-Regel: SL NICHT in den Gewinn verschieben (B-C Korrektionen stoppen aus).</summary>
    bool DisableSmartBreakeven = false,
    /// <summary>SK-Regel: Zusätzlicher Entry (halbe Position) für Staffelung 50er voll + 66.7er halb.</summary>
    bool IsAdditionalEntry = false,
    /// <summary>SK-Plan 4.7: ATR-Wert (H1 oder passender TF) zum Entry-Zeitpunkt, für Trailing-Stop-Berechnung.</summary>
    decimal? EntryAtr = null,
    /// <summary>SK-Plan 5.5: Sequenz-Identifier für Re-Entry-Budget (Symbol + Point0 + PointA). Null = kein Re-Entry-Tracking.</summary>
    string? SequenceId = null,
    /// <summary>
    /// Task 1.1 — GKL-Treffer (Königsdisziplin). True wenn der aktuelle Preis in der 50-66.7%
    /// Korrektur einer laufenden W1- oder D1-Sequenz liegt. Scorer vergibt dafür +2 Confluence.
    /// </summary>
    bool IsGklSetup = false,
    /// <summary>Task 1.1 — Timeframe des GKL-Setups (W1 oder D1) zur Anzeige im ActivityFeed.</summary>
    TimeFrame? GklTimeframe = null,
    /// <summary>
    /// Task 3.2 — Navigator-PointA der Signal-Sequenz. Wird in PositionExitState persistiert
    /// und als zweiter BE-Trigger genutzt (Preis durchbricht A → BE setzen).
    /// </summary>
    decimal? NavPointA = null,
    /// <summary>
    /// Task 4.7 — Extension 423.6% als Hard-Cap für Runner. Sobald der Runner diesen Level
    /// erreicht, wird zwangsgeschlossen (Buch: "absolute Maximalausdehnung").
    /// </summary>
    decimal? RunnerHardCap = null,
    /// <summary>
    /// Task 4.10 — Counter-Trend-Scalp-Flag. True wenn das Signal gegen den Haupt-Trend geht
    /// (Trade in die entgegengesetzte Richtung an Extension 161.8%/200%). Buch warnt: hochriskant.
    /// </summary>
    bool IsCounterTrendScalp = false,
    /// <summary>
    /// Task 4.10 — Multiplikator für Position-Size (z.B. 0.5 = halbe Position für Counter-Trend-Scalps).
    /// Null = keine Anpassung (normale SK-Position).
    /// </summary>
    decimal? PositionScaleOverride = null,
    /// <summary>
    /// v1.5.0 Phase 2 — Asymmetrisches CRV: Wenn TP1/TP2 aus der HTF-Sequenz statt LTF stammen,
    /// haelt dieses Feld den HTF-Timeframe (W1 oder D1) fuer UI-Badge "TP von D1".
    /// Null = klassisches Verhalten (TP aus LTF), kein Badge anzeigen.
    /// </summary>
    TimeFrame? TpSourceTimeframe = null);
