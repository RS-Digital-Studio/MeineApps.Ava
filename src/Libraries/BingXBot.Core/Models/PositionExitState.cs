using BingXBot.Core.Enums;

namespace BingXBot.Core.Models;

/// <summary>
/// Exit-Phase einer offenen Position im Multi-Stage Exit System.
/// Buch-konform: Initial → Tp1Hit (nach TP1 Partial Close).
/// </summary>
public enum ExitPhase
{
    /// <summary>Position offen, wartet auf TP1 oder SL.</summary>
    Initial,
    /// <summary>TP1 (161.8%) erreicht: 50% geschlossen, Rest läuft bis TP2 (200%+Buffer).</summary>
    Tp1Hit,
}

/// <summary>
/// Vollständiger Zustand einer offenen Position für das Buch-konforme Exit-System.
/// Gespeichert in ConcurrentDictionary im TradingServiceBase.
/// THREAD-SAFETY: Properties werden NUR aus dem PriceTickerLoop mutiert (sequentiell pro Service).
/// Kein paralleler Schreibzugriff erlaubt — ConcurrentDictionary sichert nur Add/Get/Remove.
/// </summary>
public class PositionExitState
{
    /// <summary>Originales Signal mit SL/TP1 (wird bei TP1-Hit modifiziert: TP auf TP2).</summary>
    public SignalResult Signal { get; set; } = null!;

    /// <summary>Aktuelle Exit-Phase.</summary>
    public ExitPhase Phase { get; set; } = ExitPhase.Initial;

    /// <summary>Entry-Preis der Position.</summary>
    public decimal EntryPrice { get; set; }

    /// <summary>Ursprüngliche Positionsgröße (für 50% Partial-Close bei TP1).</summary>
    public decimal OriginalQuantity { get; set; }

    /// <summary>Take-Profit 2 (200% Extension + 20 Pips Buffer, Buch Workflow 4.5).</summary>
    public decimal? Tp2 { get; set; }

    /// <summary>Zeitpunkt des Entries (für Time-based Exit).</summary>
    public DateTime EntryTime { get; set; } = DateTime.UtcNow;

    /// <summary>Seite der Position (Buy/Sell).</summary>
    public Side Side { get; set; }

    /// <summary>Symbol der Position.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Ob die Position bereits teilweise geschlossen wurde (TP1 Partial-Close).</summary>
    public bool PartialClosed { get; set; }

    /// <summary>
    /// Ob der Auto-Breakeven bereits gesetzt wurde.
    /// SK-Buch Masterclass: "Sobald der Preis aus deiner Korrekturbox herausläuft und das Level A
    /// signifikant durchbricht, ziehst du den Stop-Loss auf Break Even." — einziger BE-Trigger.
    /// </summary>
    public bool BreakevenSet { get; set; }

    /// <summary>
    /// Ob diese Position nach App-Neustart wiederhergestellt wurde.
    /// Time-Exit bekommt eine Karenz, da die echte Haltezeit unbekannt ist
    /// (BingX liefert kein OpenTime in GetPositionsAsync).
    /// </summary>
    public bool IsRecovered { get; set; }

    /// <summary>
    /// SK-Plan 4.6: Aktueller Trailing-Stop-Preis nach TP1 (opt-in, bei EnableTrailingStopAfterTp1).
    /// Wird pro Tick mit 1.5× ATR(H1) Abstand zum aktuellen Extrem nachgezogen.
    /// 0 = Trailing-Stop inaktiv. Nach Aktivierung ersetzt er den BE-SL aus Workflow 4.2.
    /// WIDERSPRUCH zu Buch 4.3 — daher default aus, nur via Settings opt-in aktivierbar.
    /// </summary>
    public decimal TrailingStopPrice { get; set; }

    /// <summary>
    /// SK-Plan 4.6: Bestes Extrem seit TP1 (Long: höchstes High, Short: tiefstes Low).
    /// Basis für Trailing-Stop-Nachzug.
    /// </summary>
    public decimal TrailingAnchor { get; set; }

    /// <summary>
    /// SK-Plan 5.5: Sequenz-Identifier (symbol_point0_pointA) für Re-Entry-Budget.
    /// Nach SL: Wenn diese Sequenz noch intakt (Point 0 nicht überschritten) und BC-Zone wieder erreicht,
    /// max. 1 Re-Entry mit halber Position erlaubt.
    /// </summary>
    public string? SequenceId { get; set; }

    /// <summary>
    /// SK-Plan 5.5: Ob bereits ein Re-Entry für diese Sequenz verbraucht wurde.
    /// Strikt max. 1 — verhindert Serienverluste bei kaputten Setups.
    /// </summary>
    public bool ReentryUsed { get; set; }

    /// <summary>
    /// Multi-TF Standalone: Navigator-TF des auslösenden Signals (D1/H4/H1/M5).
    /// Wird beim Trade-Close an <see cref="CompletedTrade.NavigatorTimeframe"/> weitergereicht.
    /// </summary>
    public TimeFrame NavigatorTimeframe { get; set; } = TimeFrame.H4;

    /// <summary>
    /// Task 3.2 — Navigator-PointA der Signal-Sequenz, persistiert für A-Bruch-BE-Trigger.
    /// Bei Preis-Durchbruch von PointA (Long: price ≥ A, Short: price ≤ A) wird BE gesetzt —
    /// Buch-Masterclass: "Sobald der Preis aus deiner Korrekturbox herausläuft und das Level A
    /// signifikant durchbricht, ziehst du den Stop-Loss auf Break Even."
    /// 0 = unbekannt (Legacy / Recovery ohne Signal-Metadaten).
    /// </summary>
    public decimal NavPointA { get; set; }

    /// <summary>
    /// Task 4.7 — True wenn nach TP2-Hit ein Runner-Anteil weiterläuft. SL wird dann via
    /// Trailing-ATR nachgezogen, Exit bei Trailing-Hit oder Extension4236.
    /// </summary>
    public bool RunnerActive { get; set; }

    /// <summary>Task 4.7 — Aktueller Anker für den Trailing-Stop (best-price seit TP2-Hit).</summary>
    public decimal RunnerTrailAnchor { get; set; }

    /// <summary>Task 4.7 — ATR-Basis für Trailing-Distanz (zum Entry-Zeitpunkt eingefroren).</summary>
    public decimal RunnerAtrBase { get; set; }

    /// <summary>Task 4.7 — Extension4236 als Hard-Cap für Runner (nie darüber laufen lassen).</summary>
    public decimal RunnerHardCap { get; set; }

    /// <summary>
    /// v1.2.7 Fix — Zuletzt an die Exchange gepushter Trail-SL (Runner-Phase).
    /// Ohne diesen Wert würde der nachgezogene Trail-SL nur im Memory leben; bei App-Crash
    /// wäre der BingX-SL noch auf dem initialen Preis → Runner-Gewinn verloren.
    /// Der PriceTicker-Loop vergleicht den berechneten trailSl mit diesem Feld und pusht
    /// nur bei signifikanter Bewegung (Throttle), um API-Rate-Limits zu schonen.
    /// 0 = noch nichts gepusht (Runner gerade aktiviert).
    /// </summary>
    public decimal RunnerLastPushedSl { get; set; }

    /// <summary>
    /// v1.2.7 Fix — UTC-Zeitpunkt des letzten Runner-SL-Pushes an die Exchange.
    /// Dient als zusätzliches Throttle neben dem Preis-Delta (<see cref="RunnerLastPushedSl"/>),
    /// damit bei Flat-Phasen kein permanenter API-Spam entsteht.
    /// </summary>
    public DateTime RunnerLastPushUtc { get; set; }

    // ═════════════════════════════════════════════════════════════════════
    // v1.4.0 Phase 0.6 (Finding 0.6) — TP-Place-Retry-Mechanik
    // ═════════════════════════════════════════════════════════════════════
    // Wenn nach einem Market-Fill die Position bei BingX nicht binnen 3 s sichtbar ist
    // (Funding-Settlement-Welle, Hochlast), war vor v1.4.0 der TP-Place skipped → Position
    // ungeschuetzt bis SL-Hit oder Bot-PriceTickerLoop-Fallback. Bei Bot-Crash zwischen
    // Fill und naechstem Tick: ungeschuetzt bis SL-Hit auf Mark-Price.

    /// <summary>
    /// True wenn die initialen TP-Place-Versuche fehlgeschlagen sind und der Bot in
    /// folgenden Ticks erneut versuchen soll. Wird in <c>OnBeforePriceTickerIteration</c>
    /// gepollt, max 30 s nach <see cref="PendingTpFirstAttemptUtc"/>.
    /// </summary>
    public bool PendingTpRetry { get; set; }

    /// <summary>Zaehler der bisherigen Stage-3-Retry-Versuche (zur Diagnose).</summary>
    public int PendingTpRetryCount { get; set; }

    /// <summary>UTC-Zeitstempel des ersten TP-Place-Versuchs (fuer 30-s-Timeout in Stage 3).</summary>
    public DateTime PendingTpFirstAttemptUtc { get; set; }

    // ═════════════════════════════════════════════════════════════════════
    // v1.4.0 Phase 0.2 + 0.3 (Findings 0.2/0.3) — TP1/TP2 LIMIT auf Exchange
    // ═════════════════════════════════════════════════════════════════════
    // Wenn der Bot TP1/TP2 als Reduce-Only-LIMIT auf BingX platziert (Maker-Fee), darf der
    // Bot-PriceTickerLoop NICHT parallel den TP-Hit checken und ClosePartialAsync triggern —
    // sonst Doppel-Close-Race: BingX fuellt das Limit + der Bot Market-closed nochmal mit
    // falsch berechneter Quantity (pos.Quantity nach Limit-Partial-Fill * 0.5 ≠ originale 50%).
    // Auf der Live-Seite uebernimmt BingX den Hit (LIMIT @ TP-Preis), Order-Filled-Event vom
    // User-Data-Stream triggert Phase-Transition + CompletedTrade.

    /// <summary>
    /// BingX-OrderId der TP1-Reduce-Only-LIMIT-Order. Gesetzt nach erfolgreichem
    /// <c>PlaceTpReduceOnlyLimitAsync</c>. Solange != null + Phase = Initial wird der
    /// PriceTickerLoop-TP1-Hit-Check geskippt. PaperTradingService laesst dieses Feld
    /// immer null → Bot-Pfad bleibt im Paper-Mode aktiv.
    /// </summary>
    public string? Tp1LimitOrderId { get; set; }

    /// <summary>BingX-OrderId der TP2-Reduce-Only-LIMIT-Order (analog zu <see cref="Tp1LimitOrderId"/>).</summary>
    public string? Tp2LimitOrderId { get; set; }

    /// <summary>
    /// True wenn der aktive TP von BingX nativ ueberwacht wird (kein Bot-Hit-Check noetig).
    /// Phase=Initial → Tp1LimitOrderId. Phase=Tp1Hit → Tp2LimitOrderId.
    /// </summary>
    public bool IsTpManagedByExchange =>
        Phase == ExitPhase.Initial ? !string.IsNullOrEmpty(Tp1LimitOrderId)
                                   : !string.IsNullOrEmpty(Tp2LimitOrderId);

    // === v1.7.0 Phase 16 — Cross-TF-Pyramiding (User-Ausnahme) ===
    /// <summary>Anzahl bisheriger Pyramid-Add-Ons. Bei Erstposition = 0.</summary>
    public int PyramidAddOnCount { get; set; }

    /// <summary>
    /// Liste der Pyramid-Adds (jeweils mit eigener TF, Entry-Preis und TP1/TP2). Erstposition
    /// ist NICHT in dieser Liste — sie lebt in den Top-Level-Feldern (EntryPrice, Signal.TakeProfit).
    /// </summary>
    public List<PyramidEntry> PyramidEntries { get; set; } = new();
}

/// <summary>
/// v1.7.0 Phase 16 — Ein Pyramid-Add-On (zusaetzlicher Entry auf hoeherer TF).
/// </summary>
public sealed record PyramidEntry(
    BingXBot.Core.Enums.TimeFrame Tf,
    DateTime EntryTimeUtc,
    decimal EntryPrice,
    decimal Quantity,
    decimal? TakeProfit1,
    decimal? TakeProfit2);
