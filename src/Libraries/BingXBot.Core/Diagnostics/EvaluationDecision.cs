using BingXBot.Core.Enums;

namespace BingXBot.Core.Diagnostics;

/// <summary>
/// v1.5.2 Phase 4 — Decision-Trail / Rejection-Log.
///
/// Eine pro-Evaluate-Aufruf erzeugte Diagnose-Zeile fuer den User-Question-Pfad
/// "Warum greift mein Setup nicht?". Felder reichen aus, um aus dem Dashboard heraus
/// rueckverfolgbar zu machen, in welcher SK-State-Phase die Sequenz haengt und welcher
/// Filter (falls einer) das Signal blockiert hat.
///
/// Mehrere Decisions pro Symbol/TF in kurzer Folge sind erwartet — der Aufrufer ist
/// fuer Throttling/Ringpuffer verantwortlich (siehe <see cref="DecisionTrailBuffer"/>).
/// Wird im Hot-Path erzeugt — bewusst kein Allokations-Hotspot (Records mit Reference-
/// Strings), bei <c>BotSettings.EnableDecisionTrail = false</c> wird gar nicht erst
/// gebaut/gepublished.
/// </summary>
/// <param name="Symbol">Marktsymbol z.B. "BTC-USDT".</param>
/// <param name="Tf">Navigator-Timeframe, auf dem die Evaluation lief.</param>
/// <param name="UtcTimestamp">Zeitstempel der Entscheidung (UTC).</param>
/// <param name="SequenceState">SK-Sequenz-State (Suche0/SucheA/SucheB/Aktiviert/Abgearbeitet).</param>
/// <param name="Point0">Strukturpunkt 0 (Sequenzstart) wenn vorhanden.</param>
/// <param name="PointA">Strukturpunkt A (Impuls-Ende) wenn vorhanden.</param>
/// <param name="PointB">Strukturpunkt B (Korrektur-Ende) wenn vorhanden.</param>
/// <param name="Triggered">True = Signal erzeugt, False = blockiert.</param>
/// <param name="RejectionReason">
/// Konstanten-Code aus <see cref="RejectionReasons"/>. Null wenn <see cref="Triggered"/> = true.
/// </param>
/// <param name="ConfluenceScore">Aktueller Confluence-Score (0-10).</param>
/// <param name="ConfluenceCategories">Liste der Kategorie-Namen die Score-Punkte vergeben haben.</param>
/// <param name="HardFiltersFailed">Liste der Hard-Filter-Codes die fehlgeschlagen sind (z.B. "no_htf_confluence").</param>
public sealed record EvaluationDecision(
    string Symbol,
    TimeFrame Tf,
    DateTime UtcTimestamp,
    string SequenceState,
    decimal? Point0,
    decimal? PointA,
    decimal? PointB,
    bool Triggered,
    string? RejectionReason,
    int ConfluenceScore,
    IReadOnlyList<string> ConfluenceCategories,
    IReadOnlyList<string> HardFiltersFailed);

/// <summary>
/// v1.5.2 Phase 4 — Konstanten fuer <see cref="EvaluationDecision.RejectionReason"/>.
/// Stabiler Code-Satz fuer Filter-Visualisierung in der UI ("Filter nach Reject-Reason").
/// Erweiterungen sind additiv — alte Codes bleiben gueltig.
/// </summary>
public static class RejectionReasons
{
    /// <summary>News-Blackout-Fenster aktiv (RiskSettings.NewsBlackoutMinutes).</summary>
    public const string NewsBlackout = "news_blackout";
    /// <summary>SK-State noch nicht <c>Aktiviert</c> — Sequenz lebt, aber Setup unvollstaendig.</summary>
    public const string StateNotActivated = "state_not_activated";
    /// <summary>Impuls-Distanz unter ATR × ImpulseAtrMultiplier (Strukturpunkte §2).</summary>
    public const string ImpulseBelowAtr = "impulse_below_atr";
    /// <summary>HTF-Confluence-Hard-Gate: weder GKL-Hit noch Zone-Overlap.</summary>
    public const string NoHtfConfluence = "no_htf_confluence";
    /// <summary>Confluence-Score unter <c>RiskSettings.MinConfluenceScore</c>.</summary>
    public const string ScoreBelowMin = "score_below_min";
    /// <summary>RRR (TP2-Distanz / SL-Distanz) unter Min (1.0).</summary>
    public const string RrrTooSmall = "rrr_too_small";
    /// <summary>Korrekturbox geschlossen — Wick-/Pinbar-Eintrag fehlt (RequireBoxCloseOnEntry).</summary>
    public const string BoxCloseViolated = "box_close_violated";
    /// <summary>Wick-Rejection in B-Zone fehlt (RequireWickRejectionInBZone).</summary>
    public const string MissingWickRejection = "missing_wick_rejection";
    /// <summary>HTF in Zielzone (Spec §7 B18) — LTF-Block weil HTF kurz vor Trendwende steht.</summary>
    public const string MtaTargetZoneBlock = "mta_target_zone_block";
    /// <summary>Beide Entry-Levels (Primary + Additional) bereits getriggert.</summary>
    public const string EntriesAlreadyTriggered = "entries_already_triggered";
    /// <summary>Sequenz-Startwerte unklar (Point0/PointA fehlt).</summary>
    public const string MissingStrukturpunkte = "missing_strukturpunkte";
    /// <summary>Counter-Trend-Scalp deaktiviert oder kein Hit.</summary>
    public const string CounterTrendInactive = "counter_trend_inactive";
    /// <summary>v1.6.2 Phase 12 — Order-Book-Slippage ueber Threshold (Market-Entry geblockt).</summary>
    public const string SlippageTooHigh = "slippage_too_high";
    /// <summary>v1.6.6 Phase 17 — TF wurde wegen schlechter WinRate auto-disabled.</summary>
    public const string TfAutoDisabled = "tf_auto_disabled";
    /// <summary>Sonstiger generischer Reject — Detail im Reason-Text.</summary>
    public const string Other = "other";
}
