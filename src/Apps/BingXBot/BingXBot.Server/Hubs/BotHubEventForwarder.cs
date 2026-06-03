using BingXBot.Contracts.Api;
using BingXBot.Contracts.Dto;
using BingXBot.Contracts.Services;
using Microsoft.AspNetCore.SignalR;

namespace BingXBot.Server.Hubs;

/// <summary>
/// HostedService: Subscribt beim Start auf IBotEventStream und leitet alle Events
/// per SignalR an alle verbundenen Clients weiter. Throttling und Pro-Symbol-Gruppen
/// sind hier zu implementieren (Phase 3.5).
/// </summary>
public sealed class BotHubEventForwarder : IHostedService, IDisposable
{
    private readonly IHubContext<BotHub> _hub;
    private readonly IBotEventStream _stream;
    private readonly ILogger<BotHubEventForwarder> _logger;

    // Throttle-Merker pro Symbol: letzter Ticker-Zeitstempel
    private readonly Dictionary<string, DateTime> _lastTickerPerSymbol = new();
    private readonly Lock _tickerLock = new();
    private static readonly TimeSpan TickerThrottle = TimeSpan.FromMilliseconds(1000);

    // 04.05.2026 — Throttle für PositionUpdated (max 1 Update pro {Symbol,Side} und 2 s).
    // Vorher floss jedes 5-s-Tick-Update aller offenen Positionen ungedrosselt durch SignalR
    // (bei 20 offenen Positionen = 20 SignalR-Sends/5s/Client). 2 s ist langsam genug für UI,
    // schnell genug für SL/TP/BE-Updates die separat über andere Events propagiert werden.
    private readonly Dictionary<string, DateTime> _lastPositionPerKey = new();
    private readonly Lock _positionLock = new();
    private static readonly TimeSpan PositionThrottle = TimeSpan.FromMilliseconds(2000);

    // 04.05.2026 — Log-Batching: 250 ms Buffer reduziert SignalR-Overhead bei Scan-Bursts
    // (50-200 Log-Events pro Scan-Zyklus). Bei einzelnem Eintrag wird weiterhin LogEmitted
    // gefeuert (Backwards-Compat); bei mehreren Einträgen wird LogBatch genutzt — RemoteBotEventStream
    // splittet das clientseitig in einzelne LogEmitted-Events.
    private readonly List<LogEntryDto> _logBuffer = new();
    private readonly Lock _logLock = new();
    private static readonly TimeSpan LogFlushInterval = TimeSpan.FromMilliseconds(250);
    private Timer? _logFlushTimer;

    public BotHubEventForwarder(
        IHubContext<BotHub> hub,
        IBotEventStream stream,
        ILogger<BotHubEventForwarder> logger)
    {
        _hub = hub;
        _stream = stream;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stream.BotStateChanged += OnBotStateChanged;
        _stream.TradeOpened += OnTradeOpened;
        _stream.TradeClosed += OnTradeClosed;
        _stream.PositionUpdated += OnPositionUpdated;
        _stream.EquityUpdate += OnEquityUpdate;
        _stream.MarginWarning += OnMarginWarning;
        _stream.TickerUpdate += OnTickerUpdate;
        _stream.BtcPriceUpdate += OnBtcPrice;
        _stream.ScannerResult += OnScannerResult;
        _stream.LogEmitted += OnLog;
        _stream.ActivityFeed += OnActivity;
        _stream.BacktestProgress += OnBacktestProgress;
        _stream.BacktestCompleted += OnBacktestCompleted;
        _stream.SettingsChanged += OnSettingsChanged;
        _stream.ConnectionDegraded += OnConnectionDegraded;
        _stream.EvaluationDecided += OnEvaluationDecided;
        // Periodischer Flush des Log-Buffers (alle 250 ms).
        _logFlushTimer = new Timer(_ => FlushLogBuffer(), null, LogFlushInterval, LogFlushInterval);
        _logger.LogInformation("BotHubEventForwarder gestartet");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stream.BotStateChanged -= OnBotStateChanged;
        _stream.TradeOpened -= OnTradeOpened;
        _stream.TradeClosed -= OnTradeClosed;
        _stream.PositionUpdated -= OnPositionUpdated;
        _stream.EquityUpdate -= OnEquityUpdate;
        _stream.MarginWarning -= OnMarginWarning;
        _stream.TickerUpdate -= OnTickerUpdate;
        _stream.BtcPriceUpdate -= OnBtcPrice;
        _stream.ScannerResult -= OnScannerResult;
        _stream.LogEmitted -= OnLog;
        _stream.ActivityFeed -= OnActivity;
        _stream.BacktestProgress -= OnBacktestProgress;
        _stream.BacktestCompleted -= OnBacktestCompleted;
        _stream.SettingsChanged -= OnSettingsChanged;
        _stream.ConnectionDegraded -= OnConnectionDegraded;
        _stream.EvaluationDecided -= OnEvaluationDecided;
        _logFlushTimer?.Dispose();
        _logFlushTimer = null;
        FlushLogBuffer(); // Letzte Logs noch durchsenden
        return Task.CompletedTask;
    }

    private void OnBotStateChanged(BotStateChangedDto dto) =>
        Fire(HubMethods.BotStateChanged, dto);
    private void OnTradeOpened(TradeDto dto) => Fire(HubMethods.TradeOpened, dto);
    private void OnTradeClosed(TradeDto dto) => Fire(HubMethods.TradeClosed, dto);
    private void OnPositionUpdated(PositionDto dto)
    {
        // Throttle pro {Symbol,Side}: max 1 Update / 2 s. Spürbare Bandbreiten- und Battery-Reduktion
        // auf Android-Clients bei vielen offenen Positionen.
        var key = $"{dto.Symbol}_{dto.Side}";
        var nowUtc = DateTime.UtcNow;
        lock (_positionLock)
        {
            if (_lastPositionPerKey.TryGetValue(key, out var last) && nowUtc - last < PositionThrottle)
                return;
            _lastPositionPerKey[key] = nowUtc;
        }
        Fire(HubMethods.PositionUpdated, dto);
    }
    private void OnEquityUpdate(EquityPointDto dto) => Fire(HubMethods.EquityUpdate, dto);
    private void OnMarginWarning(MarginWarningDto dto) => Fire(HubMethods.MarginWarning, dto);
    private void OnBtcPrice(TickerUpdateDto dto) => Fire(HubMethods.BtcPriceUpdate, dto);
    private void OnScannerResult(ScannerResultDto dto) => Fire(HubMethods.ScannerResult, dto);
    private void OnLog(LogEntryDto dto)
    {
        // Buffer den Eintrag; FlushLogBuffer() entscheidet ob LogEmitted (1 Eintrag) oder LogBatch (>1).
        lock (_logLock)
        {
            _logBuffer.Add(dto);
        }
    }

    private void FlushLogBuffer()
    {
        List<LogEntryDto>? batch;
        lock (_logLock)
        {
            if (_logBuffer.Count == 0) return;
            batch = _logBuffer.ToList();
            _logBuffer.Clear();
        }

        if (batch.Count == 1)
            Fire(HubMethods.LogEmitted, batch[0]);
        else
            Fire(HubMethods.LogBatch, (IReadOnlyList<LogEntryDto>)batch);
    }
    private void OnActivity(ActivityFeedDto dto) => Fire(HubMethods.ActivityFeed, dto);
    private void OnBacktestProgress(BacktestProgressDto dto) => Fire(HubMethods.BacktestProgress, dto);
    private void OnBacktestCompleted(BacktestResultDto dto) => Fire(HubMethods.BacktestCompleted, dto);
    private void OnSettingsChanged(FullSettingsDto dto) => Fire(HubMethods.SettingsChanged, dto);
    private void OnConnectionDegraded(ConnectionDegradedDto dto) => Fire(HubMethods.ConnectionDegraded, dto);
    private void OnEvaluationDecided(EvaluationDecisionDto dto) => Fire(HubMethods.EvaluationDecided, dto);

    private void OnTickerUpdate(TickerUpdateDto dto)
    {
        // Throttle: max 1 Update pro Symbol und Sekunde
        lock (_tickerLock)
        {
            if (_lastTickerPerSymbol.TryGetValue(dto.Symbol, out var last)
                && dto.TimestampUtc - last < TickerThrottle)
                return;
            _lastTickerPerSymbol[dto.Symbol] = dto.TimestampUtc;
        }
        Fire(HubMethods.TickerUpdate, dto);
    }

    private void Fire<T>(string method, T payload)
    {
        // Fire-and-forget an alle Clients. SignalR kuemmert sich um den I/O.
        // Unobserved-Exception-Schutz: wenn der Send auf einem Client crasht (Disconnect-Race,
        // OOM, Serialization), fangen wir hier — sonst wuerde die Task an TaskScheduler.
        // UnobservedTaskException eskalieren und bei Default-Config den Prozess beenden.
        _ = _hub.Clients.All.SendAsync(method, payload).ContinueWith(
            t => _logger.LogWarning(t.Exception,
                "SignalR-Send {Method} fehlgeschlagen: {Error}", method, t.Exception?.GetBaseException().Message),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Dispose() { }
}
