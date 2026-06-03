using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BingXBot.Trading.Telemetry;

/// <summary>
/// Phase 18 / H6 — Zentrale ActivitySource + Meter fuer den Bot.
/// Server-Bootstrap registriert die Source per Name (<see cref="SourceName"/>) im OpenTelemetry-Tracer
/// + Meter-Provider und exportiert nach Prometheus (<c>/metrics</c>).
/// Im Client- (Standalone-) Mode sind die Activities/Meter no-ops bis ein Listener registriert wird —
/// kein Performance-Hit ohne Subscriber.
/// </summary>
public static class BotTelemetry
{
    /// <summary>Konstanter Name der ActivitySource — Server-Bootstrap muss diesen Wert in OTel registrieren.</summary>
    public const string SourceName = "BingXBot.Trading";

    /// <summary>Activity-Source fuer Tracing (Strategy.Evaluate, RiskManager.Validate, OrderRetryPolicy etc.).</summary>
    public static readonly ActivitySource Source = new(SourceName, "1.0");

    /// <summary>Meter fuer Counter/Gauges (Trades, Decisions, Risk-Rejects, Open-Positions etc.).</summary>
    public static readonly Meter Meter = new(SourceName, "1.0");

    // Counter — Up-only-Metrics
    /// <summary>Anzahl Strategy-Evaluations pro (Symbol, Tf).</summary>
    public static readonly Counter<long> StrategyEvaluations =
        Meter.CreateCounter<long>("bingxbot.strategy.evaluations", "evaluations", "Strategy.Evaluate-Aufrufe pro Tick");
    /// <summary>Anzahl Trade-Open-Events.</summary>
    public static readonly Counter<long> TradesOpened =
        Meter.CreateCounter<long>("bingxbot.trades.opened", "trades", "Geöffnete Trades");
    /// <summary>Anzahl Trade-Close-Events (mit Tag fuer Reason: TP1/TP2/SL/BE/Manual).</summary>
    public static readonly Counter<long> TradesClosed =
        Meter.CreateCounter<long>("bingxbot.trades.closed", "trades", "Geschlossene Trades");
    /// <summary>Anzahl RiskManager-Rejects (mit Tag fuer Grund).</summary>
    public static readonly Counter<long> RiskRejects =
        Meter.CreateCounter<long>("bingxbot.risk.rejects", "rejects", "RiskManager-Ablehnungen");
    /// <summary>Anzahl Decision-Trail-Eintraege (Reject + Success).</summary>
    public static readonly Counter<long> DecisionsLogged =
        Meter.CreateCounter<long>("bingxbot.decisions.logged", "decisions", "Decision-Trail-Eintraege");
    /// <summary>Anzahl Order-Retry-Attempts (Phase 8 / A2).</summary>
    public static readonly Counter<long> OrderRetries =
        Meter.CreateCounter<long>("bingxbot.orders.retries", "retries", "Order-Retry-Versuche");

    /// <summary>
    /// Phase 18 / H6 — Helper: Startet eine Activity nur wenn ein Listener aktiv ist.
    /// Vermeidet die Allokation in Standalone-/Test-Setups ohne OTel-Listener.
    /// </summary>
    public static Activity? StartActivity(string name)
        => Source.HasListeners() ? Source.StartActivity(name) : null;
}
