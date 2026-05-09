using BingXBot.Contracts.Dto;
using BingXBot.Core.Enums;

namespace BingXBot.Contracts.Services;

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Degraded
}

/// <summary>
/// Zentraler Event-Strom fuer alle Live-Updates von Server zu Client.
/// Im Server: Wrappt BotEventBus 1:1 (keine Netz-Schicht, direkt).
/// Im Client: Wrappt SignalR-Hub-Connection.
///
/// Ersetzt die direkte Verwendung von BotEventBus in den ViewModels — die subscriben jetzt
/// gegen dieses Interface und sehen keinen Unterschied zwischen Local und Remote.
/// </summary>
public interface IBotEventStream : IDisposable
{
    // Status
    ConnectionStatus Connection { get; }
    event Action<ConnectionStatus>? ConnectionChanged;

    // Trading-Events
    event Action<BotStateChangedDto>? BotStateChanged;
    event Action<TradeDto>? TradeOpened;
    event Action<TradeDto>? TradeClosed;
    event Action<PositionDto>? PositionUpdated;
    event Action<EquityPointDto>? EquityUpdate;
    event Action<MarginWarningDto>? MarginWarning;

    // Market-Data
    event Action<TickerUpdateDto>? TickerUpdate;
    event Action<TickerUpdateDto>? BtcPriceUpdate;
    event Action<ScannerResultDto>? ScannerResult;

    // Logs & Activity
    event Action<LogEntryDto>? LogEmitted;
    event Action<ActivityFeedDto>? ActivityFeed;

    // Backtest
    event Action<BacktestProgressDto>? BacktestProgress;
    event Action<BacktestResultDto>? BacktestCompleted;

    // Settings-Sync (Multi-Client): Server pushed SettingsChanged wenn ein Client die Settings
    // aendert, alle anderen Clients sehen den neuen Snapshot live (ohne GET-Polling).
    event Action<FullSettingsDto>? SettingsChanged;

    // Server-Health: wird vom ServerHealthWatchdog (IHostedService auf dem Pi) gepushed, wenn
    // sich der Live-Exchange-Verbindungsstatus aendert (BingX-WS weg, Rest-Client disconnected
    // waehrend der Bot im Live-Mode laeuft). Im Local-Mode feuert das Event gar nicht — dort
    // sieht der User Live-Engine-Probleme direkt im Log.
    event Action<ConnectionDegradedDto>? ConnectionDegraded;

    // v1.5.2 Phase 4 — Decision-Trail. Server pusht pro Strategy-Evaluation eine Decision
    // (Reject mit Reason oder Success). Client kann die im Live-Stream zeigen ohne GET-Polling.
    event Action<EvaluationDecisionDto>? EvaluationDecided;

    /// <summary>
    /// Phase 18 / H2 — News-Service-Health. Wird gepushed wenn der News-Calendar-Service
    /// degradiert (≥5 Failures in Folge bei <c>RiskManager.ResolveActiveNewsBlackoutAsync</c>)
    /// oder sich erholt. UI zeigt das als Banner analog ConnectionDegraded.
    /// </summary>
    event Action<NewsServiceDegradedDto>? NewsServiceDegraded;

    /// <summary>Startet die Verbindung (Client-Remote: SignalR Connect; Server-Local: No-Op).</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Beendet die Verbindung.</summary>
    Task StopAsync(CancellationToken ct = default);

    Task SubscribeSymbolAsync(string symbol, CancellationToken ct = default);
    Task UnsubscribeSymbolAsync(string symbol, CancellationToken ct = default);
    Task SetLogFilterAsync(LogLevel minLevel, CancellationToken ct = default);
}
