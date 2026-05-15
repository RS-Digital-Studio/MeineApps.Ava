using BingXBot.Core.Enums;
using BingXBot.Core.Models;

namespace BingXBot.Trading;

/// <summary>
/// Zentraler Event-Bus für die Kommunikation zwischen ViewModels.
/// Alle ViewModels können Events publizieren/subscriben ohne direkte Referenzen.
/// Registriert als Singleton im DI-Container.
/// Performance: PublishLog prüft HasLogSubscribers VOR Allokation der LogEntry.
/// Caller sollten HasLogSubscribers nutzen um String-Interpolation zu vermeiden.
/// </summary>
public class BotEventBus
{
    /// <summary>Trade abgeschlossen (von Bot oder Backtest).</summary>
    public event EventHandler<CompletedTrade>? TradeCompleted;

    /// <summary>Log-Eintrag (von Bot, Backtest, Scanner etc.).</summary>
    public event EventHandler<LogEntry>? LogEmitted;

    /// <summary>Bot-Status geändert (Running, Paused, Stopped etc.).</summary>
    public event EventHandler<BotState>? BotStateChanged;

    /// <summary>Backtest abgeschlossen mit Ergebnissen.</summary>
    public event EventHandler<BacktestCompletedArgs>? BacktestCompleted;

    /// <summary>Desktop-Benachrichtigung angefordert (Trade-Events, Warnungen).</summary>
    public event EventHandler<NotificationArgs>? NotificationRequested;

    /// <summary>Margin-Warnung: Position nähert sich Liquidationspreis.</summary>
    public event EventHandler<MarginWarningArgs>? MarginWarning;

    /// <summary>Trading-Modus gewechselt (true = Paper, false = Live). Für Statusleiste im MainViewModel.</summary>
    public event EventHandler<bool>? TradingModeChanged;

    /// <summary>Multi-TF Standalone: SK-Ampel-Status pro Navigator-TF (einmal pro Scan-Zyklus).</summary>
    public event EventHandler<Dictionary<TimeFrame, string>>? SkAmpelUpdated;

    // ══════════════════════════════════════════════════════════════════════
    // Live-Events fuer Remote-Clients (v1.3.0 Fix K1):
    // Ohne diese Events sehen Desktop/Android im Remote-Modus weder Positionen live,
    // noch Equity-Updates, noch Scanner-Ergebnisse. Der BotHubEventForwarder
    // subscribed LocalBotEventStream — und LocalBotEventStream subscribed diese
    // Events hier im EventBus.
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Position neu eroeffnet (Paper oder Live). Feuert einmal beim Entry.</summary>
    public event EventHandler<TradeOpenedArgs>? TradeOpened;

    /// <summary>Position-Snapshot mit SL/TP/Liq/BreakevenArmed-Meta. Feuert pro Position im PriceTickerLoop (alle 5 s).</summary>
    public event EventHandler<PositionSnapshotArgs>? PositionUpdated;

    /// <summary>Equity-Punkt (fuer Live-Equity-Chart). Feuert nach Tradeabschluss oder nach PriceTickerLoop-Iteration.</summary>
    public event EventHandler<EquityPoint>? EquityUpdate;

    /// <summary>Ticker-Preis-Update fuer offene Positionen (keine Scanner-Tickers — das waere Lawine).</summary>
    public event EventHandler<Ticker>? TickerUpdate;

    /// <summary>BTC-USDT-Preis separat fuer den Dashboard-Ticker.</summary>
    public event EventHandler<Ticker>? BtcPriceUpdate;

    /// <summary>Scanner-Sweep abgeschlossen: eine Liste aller Kandidaten pro Navigator-TF.
    /// Client rendert das als eine Tabelle pro TF — deshalb ein Event pro Sweep, nicht pro Symbol.</summary>
    public event EventHandler<ScannerSweepArgs>? ScannerSweep;

    /// <summary>
    /// v1.5.2 Phase 4 — Decision-Trail. Feuert pro Strategy-Evaluation mit der gefaellten
    /// Entscheidung (Reject mit Reason oder Success). Subscriber: <c>DecisionTrailBuffer</c>
    /// (In-Memory-Ringpuffer 5000), spaeter Hub-Forwarder fuer Remote-UI.
    /// </summary>
    public event EventHandler<BingXBot.Core.Diagnostics.EvaluationDecision>? EvaluationDecided;

    /// <summary>
    /// Phase 18 / H2 — News-Service-Health-Edge-Transition. Args: (isDegraded, failureCount, reason).
    /// Subscriber: LocalBotEventStream → SignalR-Hub → UI-Banner.
    /// </summary>
    public event EventHandler<(bool IsDegraded, int FailureCount, string? Reason)>? NewsServiceHealthChanged;

    /// <summary>
    /// Watchdog-Event: pro Scan-Iteration (Success oder Failure) gefeuert. Subscriber:
    /// <c>StaleEngineDetector</c> nutzt das als zuverlaessigen Activity-Indikator (statt
    /// nur ScannerResult/TradeOpened) — entdeckt auch Hangs in denen der Scan-Loop selbst
    /// stillschweigend stehenbleibt.
    /// </summary>
    public event EventHandler<ScanCycleEventArgs>? ScanCycleCompleted;

    /// <summary>Publiziert einen Scan-Cycle-Abschluss (success + duration + optionaler error).</summary>
    public void PublishScanCycle(bool success, double durationSeconds, string? errorMessage) =>
        ScanCycleCompleted?.Invoke(this, new ScanCycleEventArgs(DateTime.UtcNow, success, errorMessage, durationSeconds));

    /// <summary>Phase 18 / H2 — Publish-Helper fuer den News-Service-Health-Event.</summary>
    public void PublishNewsServiceHealthChanged(bool isDegraded, int failureCount, string? reason)
        => NewsServiceHealthChanged?.Invoke(this, (isDegraded, failureCount, reason));

    /// <summary>Ob mindestens ein Subscriber für LogEmitted registriert ist. Prüfen VOR LogEntry-Allokation.</summary>
    public bool HasLogSubscribers => LogEmitted != null;

    public void PublishTrade(CompletedTrade trade) => TradeCompleted?.Invoke(this, trade);

    /// <summary>Publiziert einen Log-Eintrag. Wird ignoriert wenn kein Subscriber registriert ist.</summary>
    public void PublishLog(LogEntry entry)
    {
        LogEmitted?.Invoke(this, entry);
    }

    public void PublishBotState(BotState state) => BotStateChanged?.Invoke(this, state);
    public void PublishBacktestCompleted(BacktestCompletedArgs args) => BacktestCompleted?.Invoke(this, args);

    /// <summary>Sendet eine Desktop-Benachrichtigung (Titel + Nachricht).</summary>
    public void PublishNotification(string title, string message) =>
        NotificationRequested?.Invoke(this, new NotificationArgs(title, message));

    /// <summary>Sendet eine Margin-Warnung für eine Position.</summary>
    public void PublishMarginWarning(string symbol, decimal currentPrice, decimal liquidationPrice, decimal distancePercent) =>
        MarginWarning?.Invoke(this, new MarginWarningArgs(symbol, currentPrice, liquidationPrice, distancePercent));

    /// <summary>Trading-Modus Wechsel melden (Paper/Live) — MainViewModel hört mit für Statusleiste.</summary>
    public void PublishTradingMode(bool isPaper) => TradingModeChanged?.Invoke(this, isPaper);

    /// <summary>Multi-TF Standalone: SK-Ampel pro TF. Dict mit einer Zeile pro aktiver Navigator-TF.</summary>
    public void PublishSkAmpel(Dictionary<TimeFrame, string> ampel) =>
        SkAmpelUpdated?.Invoke(this, ampel);

    /// <summary>Feuert wenn eine neue Position eroeffnet wurde. <paramref name="navTf"/> = Navigator-TF des ausloesenden Signals.</summary>
    public void PublishTradeOpened(Position p, TimeFrame navTf) =>
        TradeOpened?.Invoke(this, new TradeOpenedArgs(p, navTf));

    /// <summary>Position-Live-Update inkl. SL/TP/Liq/BE-Meta (nicht in Position enthalten).</summary>
    public void PublishPositionUpdated(PositionSnapshotArgs args) =>
        PositionUpdated?.Invoke(this, args);

    /// <summary>Equity-Chart-Punkt. Pragmatisch: nach Tradeabschluss ODER periodisch (z.B. 1x/min).</summary>
    public void PublishEquity(EquityPoint point) => EquityUpdate?.Invoke(this, point);

    /// <summary>Ticker fuer ein konkretes (offenes) Symbol — NICHT fuer Scanner-Sweep.</summary>
    public void PublishTicker(Ticker t) => TickerUpdate?.Invoke(this, t);

    /// <summary>BTC-USDT Preis-Update (separates Event fuer Dashboard-Ticker).</summary>
    public void PublishBtcPrice(Ticker t) => BtcPriceUpdate?.Invoke(this, t);

    /// <summary>Scanner-Sweep: alle Kandidaten eines Navigator-TF-Scans in einem Rutsch.</summary>
    public void PublishScannerSweep(ScannerSweepArgs args) => ScannerSweep?.Invoke(this, args);

    /// <summary>v1.5.2 Phase 4 — Decision-Trail-Eintrag publizieren. No-op wenn keine Subscriber.</summary>
    public void PublishEvaluationDecision(BingXBot.Core.Diagnostics.EvaluationDecision decision) =>
        EvaluationDecided?.Invoke(this, decision);

    /// <summary>True wenn mindestens ein Subscriber fuer EvaluationDecided. Caller checkt das, bevor Build aufwendiger wird.</summary>
    public bool HasEvaluationDecidedSubscribers => EvaluationDecided != null;
}

/// <summary>Argumente für das BacktestCompleted-Event.</summary>
public class BacktestCompletedArgs
{
    public List<CompletedTrade> Trades { get; set; } = new();
    public string StrategyName { get; set; } = "";
    public string Symbol { get; set; } = "";
}

/// <summary>Desktop-Benachrichtigung.</summary>
public record NotificationArgs(string Title, string Message);

/// <summary>
/// Argumente fuer <see cref="BotEventBus.ScanCycleCompleted"/>. Wird einmal pro
/// <c>RunLoopAsync</c>-Iteration gefeuert — auch bei Exceptions (Success=false + ErrorMessage).
/// Damit kann der <c>StaleEngineDetector</c> Hangs auf der Scan-Loop-Ebene direkt erkennen,
/// statt indirekt ueber ausbleibende ScannerResult-/TradeOpened-Events zu schliessen.
/// </summary>
public record ScanCycleEventArgs(
    DateTime UtcTimestamp,
    bool Success,
    string? ErrorMessage,
    double DurationSeconds);

/// <summary>
/// Argumente fuer <see cref="BotEventBus.TradeOpened"/>: Position + Navigator-TF des ausloesenden Signals.
/// Vorher wurde nur die Position gepublished, der Remote-Stream hat dann hardcoded H4 als TF
/// gemeldet — Multi-TF-Trades (M15/H1/D1) erschienen im Client-UI faelschlich als H4.
/// </summary>
public record TradeOpenedArgs(Position Position, TimeFrame NavigatorTimeframe);

/// <summary>Margin-Warnung: Position nähert sich Liquidation.</summary>
public record MarginWarningArgs(string Symbol, decimal CurrentPrice, decimal LiquidationPrice, decimal DistancePercent);

/// <summary>
/// Position-Snapshot fuer Remote-UI inkl. Meta, das nicht Teil von <see cref="Position"/> ist:
/// StopLoss/TakeProfit (aus PositionExitState), LiquidationPrice (aus Exchange) und ob
/// Smart-Breakeven aktiviert wurde (A-Bruch erreicht). Wird pro Position + pro Ticker-Loop-Iteration
/// gepublished — Hub-Forwarder drosselt dann auf 1 Update/s/Symbol.
/// </summary>
public record PositionSnapshotArgs(
    Position Position,
    decimal? StopLoss,
    decimal? TakeProfit,
    decimal? LiquidationPrice,
    bool IsSmartBreakevenArmed,
    string? StrategyName);

/// <summary>
/// Scanner-Sweep: Ein Event pro Navigator-TF-Durchlauf mit der kompletten Candidate-Liste.
/// Enthaelt auch einen SuggestedSide-Hint pro Symbol (aus <see cref="ScanResult.SetupType"/>
/// abgeleitet), damit die UI direkt Long/Short unterscheiden kann.
/// </summary>
public record ScannerSweepArgs(
    TimeFrame NavigatorTimeframe,
    IReadOnlyList<ScannerCandidate> Candidates);

/// <summary>
/// Ein Kandidat aus dem Scanner-Sweep — vor Mapping auf den Contract-DTO, damit BotEventBus
/// keine Contracts-Abhaengigkeit hat. LocalBotEventStream mappt das auf <c>ScannerSymbolDto</c>.
/// </summary>
public record ScannerCandidate(
    string Symbol,
    decimal Price,
    decimal Volume24h,
    decimal PriceChangePercent,
    int Score,
    string? SuggestedSide,
    string? Reason);
